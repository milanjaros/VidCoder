﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Resources;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using HandBrake.ApplicationServices.Interop.Json.Encode;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Practices.Unity;
using Newtonsoft.Json;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoderCommon;
using VidCoderCommon.Model;

namespace VidCoder.View
{
	public partial class Main : Window, IMainView
	{
		private MainViewModel viewModel;
		private ProcessingService processingService = Ioc.Get<ProcessingService>();
		private OutputPathService outputVM = Ioc.Get<OutputPathService>();
		private StatusService statusService = Ioc.Get<StatusService>();

		private bool tabsVisible = false;

		private Storyboard presetGlowStoryboard;
		private Storyboard pickerGlowStoryboard;

		public static System.Windows.Threading.Dispatcher TheDispatcher;

		public event EventHandler<RangeFocusEventArgs> RangeControlGotFocus;

		public Main()
		{
			Ioc.Container.RegisterInstance(typeof(Main), this, new ContainerControlledLifetimeManager());
			this.InitializeComponent();

			this.RefreshQueueColumns();
			this.LoadCompletedColumnWidths();

			if (CommonUtilities.DebugMode)
			{
				MenuItem queueFromJsonItem = new MenuItem {Header = "Queue job from JSON..."};
				queueFromJsonItem.Click += (sender, args) =>
				{
					EncodeJobViewModel jobViewModel = this.viewModel.CreateEncodeJobVM();
					DebugEncodeJsonDialog dialog = new DebugEncodeJsonDialog();
					dialog.ShowDialog();

					if (!string.IsNullOrWhiteSpace(dialog.EncodeJson))
					{
						try
						{
							JsonEncodeObject encodeObject = JsonConvert.DeserializeObject<JsonEncodeObject>(dialog.EncodeJson);

							jobViewModel.DebugEncodeJsonOverride = dialog.EncodeJson;
							jobViewModel.Job.OutputPath = encodeObject.Destination.File;
							jobViewModel.Job.SourcePath = encodeObject.Source.Path;

							this.processingService.Queue(jobViewModel);
						}
						catch (Exception exception)
						{
							MessageBox.Show(this, "Could not parse encode JSON:" + Environment.NewLine + Environment.NewLine + exception.ToString());
						}
					}
				};

				this.toolsMenu.Items.Add(new Separator());
				this.toolsMenu.Items.Add(queueFromJsonItem);
			}

			this.DataContextChanged += this.OnDataContextChanged;
			TheDispatcher = this.Dispatcher;

			this.presetGlowEffect.Opacity = 0.0;
			this.pickerGlowEffect.Opacity = 0.0;
			this.statusText.Opacity = 0.0;

			NameScope.SetNameScope(this, new NameScope());
			this.RegisterName("PresetGlowEffect", this.presetGlowEffect);
			this.RegisterName("PickerGlowEffect", this.pickerGlowEffect);
			this.RegisterName("StatusText", this.statusText);

			var storyboard = (Storyboard)this.FindResource("statusTextStoryboard");
			storyboard.Completed += (sender, args) =>
				{
					this.statusText.Visibility = Visibility.Collapsed;
				};

			var presetGlowFadeUp = new DoubleAnimation
			{
				From = 0.0,
				To = 1.0,
				Duration = new Duration(TimeSpan.FromSeconds(0.1))
			};

			var presetGlowFadeDown = new DoubleAnimation
			{
				From = 1.0,
				To = 0.0,
				BeginTime = TimeSpan.FromSeconds(0.1),
				Duration = new Duration(TimeSpan.FromSeconds(1.6))
			};

			this.presetGlowStoryboard = new Storyboard();
			this.presetGlowStoryboard.Children.Add(presetGlowFadeUp);
			this.presetGlowStoryboard.Children.Add(presetGlowFadeDown);

			Storyboard.SetTargetName(presetGlowFadeUp, "PresetGlowEffect");
			Storyboard.SetTargetProperty(presetGlowFadeUp, new PropertyPath("Opacity"));
			Storyboard.SetTargetName(presetGlowFadeDown, "PresetGlowEffect");
			Storyboard.SetTargetProperty(presetGlowFadeDown, new PropertyPath("Opacity"));

			var pickerGlowFadeUp = new DoubleAnimation
			{
				From = 0.0,
				To = 1.0,
				Duration = new Duration(TimeSpan.FromSeconds(0.1))
			};

			var pickerGlowFadeDown = new DoubleAnimation
			{
				From = 1.0,
				To = 0.0,
				BeginTime = TimeSpan.FromSeconds(0.1),
				Duration = new Duration(TimeSpan.FromSeconds(1.6))
			};

			this.pickerGlowStoryboard = new Storyboard();
			this.pickerGlowStoryboard.Children.Add(pickerGlowFadeUp);
			this.pickerGlowStoryboard.Children.Add(pickerGlowFadeDown);

			Storyboard.SetTargetName(pickerGlowFadeUp, "PickerGlowEffect");
			Storyboard.SetTargetProperty(pickerGlowFadeUp, new PropertyPath("Opacity"));
			Storyboard.SetTargetName(pickerGlowFadeDown, "PickerGlowEffect");
			Storyboard.SetTargetProperty(pickerGlowFadeDown, new PropertyPath("Opacity"));

			this.Loaded += (e, o) =>
			{
				this.RestoredWindowState = this.WindowState;
			};

			this.statusService.MessageShown += (o, e) =>
			{
				this.ShowStatusMessage(e.Value);
			};
		}

		public WindowState RestoredWindowState { get; set; }

		public void ShowBalloonMessage(string title, string message)
		{
			if (this.trayIcon.Visibility == Visibility.Visible)
			{
				this.trayIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
			}
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.viewModel = this.DataContext as MainViewModel;
			this.viewModel.AnimationStarted += this.ViewModelAnimationStarted;
			this.viewModel.View = this;
			this.processingService.PropertyChanged += (sender2, e2) =>
			    {
					if (e2.PropertyName == nameof(this.processingService.CompletedItemsCount))
					{
						this.RefreshQueueTabs();
					}
			    };

			this.RefreshQueueTabs();
			this.SetupWindowsMenu();
		}

		private void ViewModelAnimationStarted(object sender, EventArgs<string> e)
		{
			if (e.Value == "PresetGlowHighlight")
			{
				this.presetGlowStoryboard.Begin(this);
			}
			else if (e.Value == "PickerGlowHighlight")
			{
				this.pickerGlowStoryboard.Begin(this);
			}
		}

		void IMainView.SaveQueueColumns()
		{
			this.SaveQueueColumns();
		}

		private void SaveQueueColumns()
		{
			var queueColumnsBuilder = new StringBuilder();
			List<Tuple<string, double>> columns = Utilities.ParseQueueColumnList(Config.QueueColumns);
			for (int i = 0; i < columns.Count; i++)
			{
				queueColumnsBuilder.Append(columns[i].Item1);
				queueColumnsBuilder.Append(":");
				queueColumnsBuilder.Append(this.queueGridView.Columns[i].ActualWidth);

				if (i != columns.Count - 1)
				{
					queueColumnsBuilder.Append("|");
				}
			}

			Config.QueueColumns = queueColumnsBuilder.ToString();
		}

		void IMainView.ApplyQueueColumns()
		{
			this.RefreshQueueColumns();
		}

		private void RefreshQueueColumns()
		{
			this.queueGridView.Columns.Clear();
			var resources = new ResourceManager("VidCoder.Resources.CommonRes", typeof(CommonRes).Assembly);

			List<Tuple<string, double>> columns = Utilities.ParseQueueColumnList(Config.QueueColumns);
			foreach (Tuple<string, double> column in columns)
			{
				var queueColumn = new GridViewColumn
				{
					Header = resources.GetString("QueueColumnName" + column.Item1),
					CellTemplate = this.Resources["QueueTemplate" + column.Item1] as DataTemplate,
					Width = column.Item2
				};

				this.queueGridView.Columns.Add(queueColumn);
			}

			var lastColumn = new GridViewColumn
			{
				CellTemplate = this.Resources["QueueRemoveTemplate"] as DataTemplate,
				Width = Config.QueueLastColumnWidth
			};
			this.queueGridView.Columns.Add(lastColumn);
		}

		private void LoadCompletedColumnWidths()
		{
			string columnWidthsString = Config.CompletedColumnWidths;

			if (string.IsNullOrEmpty(columnWidthsString))
			{
				return;
			}

			string[] columnWidths = columnWidthsString.Split('|');
			for (int i = 0; i < this.completedGridView.Columns.Count; i++)
			{
				if (i < columnWidths.Length)
				{
					double width = 0;
					double.TryParse(columnWidths[i], out width);

					if (width > 0)
					{
						this.completedGridView.Columns[i].Width = width;
					}
				}
			}
		}

		void IMainView.SaveCompletedColumnWidths()
		{
			this.SaveCompletedColumnWidths();
		}

		private void SaveCompletedColumnWidths()
		{
			var completedColumnsBuilder = new StringBuilder();
			for (int i = 0; i < this.completedGridView.Columns.Count; i++)
			{
				completedColumnsBuilder.Append(this.completedGridView.Columns[i].ActualWidth);

				if (i != this.completedGridView.Columns.Count - 1)
				{
					completedColumnsBuilder.Append("|");
				}
			}

			Config.CompletedColumnWidths = completedColumnsBuilder.ToString();
		}

		private void RefreshQueueTabs()
		{
			if (this.processingService.CompletedItemsCount > 0 && !this.tabsVisible)
			{
				this.queueTab.Visibility = Visibility.Visible;
				this.completedTab.Visibility = Visibility.Visible;
				this.clearCompletedQueueItemsButton.Visibility = Visibility.Visible;
				this.queueItemsTabControl.BorderThickness = new Thickness(1);
				//this.tabsArea.Margin = new Thickness(6,6,6,0);

				this.tabsVisible = true;
				return;
			}

			if (this.processingService.CompletedItemsCount == 0 && this.tabsVisible)
			{
				this.queueTab.Visibility = Visibility.Collapsed;
				this.completedTab.Visibility = Visibility.Collapsed;
				this.clearCompletedQueueItemsButton.Visibility = Visibility.Collapsed;
				this.queueItemsTabControl.BorderThickness = new Thickness(0);
				//this.tabsArea.Margin = new Thickness(0,6,0,0);

				this.processingService.SelectedTabIndex = ProcessingService.QueuedTabIndex;

				this.tabsVisible = false;
				return;
			}
		}

		private void SetupWindowsMenu()
		{
			foreach (WindowMenuItemViewModel itemViewModel in this.viewModel.WindowMenuItems)
			{
				MenuItem item = new MenuItem
				{
					IsCheckable = true,
					Header = itemViewModel.Definition.MenuLabel,
					InputGestureText = itemViewModel.Definition.InputGestureText,
					Command = itemViewModel.Command
				};

				item.DataContext = itemViewModel;

				item.SetBinding(MenuItem.IsCheckedProperty, nameof(itemViewModel.IsOpen));
				item.SetBinding(UIElement.IsEnabledProperty, nameof(itemViewModel.CanOpen));

				this.windowsMenu.Items.Add(item);
			}
		}

		protected void HandleCompletedItemDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var encodeResultVM = ((ListViewItem)sender).Content as EncodeResultViewModel;
			if (encodeResultVM.EncodeResult.Succeeded)
			{
				string resultFile = encodeResultVM.EncodeResult.Destination;

				if (File.Exists(resultFile))
				{
					this.ShowStatusMessage(MainRes.PlayingVideoMessage);
					FileService.Instance.PlayVideo(encodeResultVM.EncodeResult.Destination);
				}
				else
				{
					MessageBox.Show(string.Format(MainRes.FileDoesNotExist, resultFile));
				}
			}
		}

		private void ProgressMouseEnter(object sender, MouseEventArgs e)
		{
			this.encodeProgressDetailsPopup.IsOpen = true;
		}

		private void ProgressMouseLeave(object sender, MouseEventArgs e)
		{
			this.encodeProgressDetailsPopup.IsOpen = false;
		}

		private void StartTimeGotFocus(object sender, RoutedEventArgs e)
		{
			this.RangeControlGotFocus?.Invoke(this, new RangeFocusEventArgs { GotFocus = true, RangeType = VideoRangeType.Seconds, Start = true });
		}

		private void EndTimeGotFocus(object sender, RoutedEventArgs e)
		{
			this.RangeControlGotFocus?.Invoke(this, new RangeFocusEventArgs { GotFocus = true, RangeType = VideoRangeType.Seconds, Start = false });
		}

		private void FramesStartGotFocus(object sender, RoutedEventArgs e)
		{
			this.RangeControlGotFocus?.Invoke(this, new RangeFocusEventArgs { GotFocus = true, RangeType = VideoRangeType.Frames, Start = true });
		}

		private void FramesEndGotFocus(object sender, RoutedEventArgs e)
		{
			this.RangeControlGotFocus?.Invoke(this, new RangeFocusEventArgs { GotFocus = true, RangeType = VideoRangeType.Frames, Start = false });
		}

		private void DestinationReadCoverMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.destinationEditBox.Focus();
		}

		private void DestinationEditBoxGotFocus(object sender, RoutedEventArgs e)
		{
			this.outputVM.EditingDestination = true;

			string path = this.outputVM.OutputPath;
			string fileName = Path.GetFileName(path);

			if (fileName == string.Empty)
			{
				this.destinationEditBox.Select(path.Length, 0);
			}
			else
			{
				int selectStart = path.Length - fileName.Length;

				string extension = Path.GetExtension(path);
				if (extension == string.Empty)
				{
					this.destinationEditBox.Select(selectStart, path.Length - selectStart);
				}
				else
				{
					this.destinationEditBox.Select(selectStart, path.Length - selectStart - extension.Length);
				}
			}

			this.outputVM.OldOutputPath = this.outputVM.OutputPath;
		}

		private void DestinationEditBoxLostFocus(object sender, RoutedEventArgs e)
		{
			this.StopEditing();
		}

		private void DestinationEditBoxPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				this.StopEditing();
			}
		}

		private void OnWindowDeactivated(object sender, EventArgs e)
		{
			if (this.outputVM.EditingDestination)
			{
				this.StopEditing();
			}
		}

		private void StopEditing()
		{
			this.destinationEditBox.SelectionStart = 0;
			this.destinationEditBox.SelectionLength = 0;
			this.Dispatcher.BeginInvoke(new Action(() =>
			    {
					if (this.destinationEditBox.IsFocused)
					{
						this.outputPathBrowseButton.Focus();
					}
			    }));

			this.outputVM.EditingDestination = false;
			this.outputVM.SetManualOutputPath(this.outputVM.OutputPath, this.outputVM.OldOutputPath);
		}

		private void ShowStatusMessage(string message)
		{
			DispatchUtilities.BeginInvoke(() =>
			    {
			        this.statusTextBlock.Text = message;
					this.statusText.Visibility = Visibility.Visible;
			        var storyboard = (Storyboard) this.FindResource("statusTextStoryboard");
					storyboard.Stop();
			        storyboard.Begin();
			    });
		}

		private void Window_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Point hitPoint = e.GetPosition(this);

			if (this.outputVM.EditingDestination && !this.HitElement(this.destinationEditBox, hitPoint))
			{
				this.StopEditing();
			}

			if (this.viewModel.SourceSelectionExpanded && !this.HitElement(this.sourceSelectionMenu, hitPoint))
			{
				this.viewModel.SourceSelectionExpanded = false;
			}
		}

		private void Window_StateChanged(object sender, EventArgs e)
		{
			if (this.WindowState == WindowState.Maximized || this.WindowState == WindowState.Normal)
			{
				this.RestoredWindowState = this.WindowState;
			}

			if (this.viewModel != null)
			{
				this.viewModel.RefreshTrayIcon(this.WindowState == WindowState.Minimized);
				if (this.viewModel.ShowTrayIcon)
				{
					this.Hide();
				}
			}
		}

		private bool HitElement(FrameworkElement element, Point clickedPoint)
		{
			Point relativePoint = this.destinationEditBox.TransformToAncestor(this).Transform(new Point(0, 0));

			return
				clickedPoint.X >= relativePoint.X && clickedPoint.X <= relativePoint.X + element.ActualWidth &&
				clickedPoint.Y >= relativePoint.Y && clickedPoint.Y <= relativePoint.Y + element.ActualHeight;
		}
	}
}
