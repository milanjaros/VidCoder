﻿using System;
using HandBrake.ApplicationServices.Interop.Json.Scan;

namespace VidCoderCommon
{
	using System.ServiceModel;

	public interface IHandBrakeWorkerCallback
	{
		[OperationContract(IsOneWay = true)]
		void OnScanComplete(string scanJson);

	    [OperationContract(IsOneWay = true)]
	    void OnScanProgress(float fractionComplete);

		[OperationContract(IsOneWay = true)]
		void OnEncodeStarted();

		[OperationContract(IsOneWay = true)]
		void OnEncodeProgress(float averageFrameRate, float currentFrameRate, TimeSpan estimatedTimeLeft, float fractionComplete, int passId, int pass, int passCount);

		[OperationContract(IsOneWay = true)]
		void OnEncodeComplete(bool error);

		[OperationContract(IsOneWay = true)]
		void OnMessageLogged(string message);

		[OperationContract(IsOneWay = true)]
		void OnVidCoderMessageLogged(string message);

		[OperationContract(IsOneWay = true)]
		void OnErrorLogged(string message);

		[OperationContract(IsOneWay = true)]
		void OnException(string exceptionString);
	}
}
