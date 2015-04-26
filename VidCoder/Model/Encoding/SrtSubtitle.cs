﻿namespace VidCoder.Model.Encoding
{
    public class SrtSubtitle
    {
        public string CharacterCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the subtitle track should be marked as default.
        /// </summary>
        public bool Default { get; set; }

		/// <summary>
		/// Gets or sets a value indicating the subtitle track should be burned in.
		/// </summary>
		public bool BurnedIn { get; set; }

        public string FileName { get; set; }

        public string LanguageCode { get; set; }

        public int Offset { get; set; }

        public SrtSubtitle Clone()
        {
            return new SrtSubtitle
                       {
                           Default = this.Default,
						   BurnedIn = this.BurnedIn,
                           FileName = this.FileName, 
                           LanguageCode = this.LanguageCode, 
                           CharacterCode = this.CharacterCode, 
                           Offset = this.Offset
                       };
        }
    }
}