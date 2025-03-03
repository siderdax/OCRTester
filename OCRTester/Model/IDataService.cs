﻿using OCRTester.Model.Settings;
using OCRTester.Model.Utility;

namespace OCRTester.Model
{
    public interface IDataService
    {
        WindowHandler WinHandler { get; }
        Config Config { get; }
    }
}
