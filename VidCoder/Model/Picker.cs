﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
    public class Picker
    {
        /// <summary>
        /// Gets or sets a value indicating whether this preset is the special "None" preset
        /// </summary>
        public bool IsNone { get; set; }

        public string Name { get; set; }

        public AutoAudioType Audio { get; set; }

        // Default "und"
        public string AudioLanguageCode { get; set; }

        // Applies only with AutoAudioType.Language
        public bool AudioLanguageAll { get; set; }

        public AutoSubtitleType Subtitle { get; set; }

        // Applies only with AutoSubtitleType.ForeignAudioSearch
        public bool SubtitleForeignBurnIn { get; set; }

        // Applies only with AutoSubtitleType.Language
        // Default "und"
        public string SubtitleLanguageCode { get; set; }

        // Applies only with AutoSubtitleType.Language
        public bool SubtitleLanguageAll { get; set; }

        // Applies only with AutoSubtitleType.Language
        // Default true
        public bool SubtitleLanguageOnlyIfDifferent { get; set; }

        // Applies only with AutoSubtitleType.Language
        public bool SubtitleLanguageDefault { get; set; }

        // Applies only with AutoSubtitleType.Language
        public bool SubtitleLanguageBurnIn { get; set; }
    }
}
