using System;
using System.Globalization;

namespace SebTruck
{
    // Plain .NET so the regression tests can run it.
    internal static class FancyGauge
    {
        internal const double MilesPerKm = 0.621371;

        // 270 degrees clockwise from bottom-left; radians, y up.
        internal static double DialAngle(double value01)
        {
            double v = Math.Max(0.0, Math.Min(1.0, value01));
            return (225.0 - 270.0 * v) * Math.PI / 180.0;
        }

        internal static double DistanceKm(float speedKmh, float deltaSeconds)
        {
            return speedKmh > 0f && deltaSeconds > 0f ? speedKmh * (double)deltaSeconds / 3600.0 : 0.0;
        }

        // Index 0 is the lowest wheel. A wheel turns only while every wheel below it passes 9 -> 0.
        internal static void OdometerWheels(double value, int[] digits, double[] rolls)
        {
            double v = Math.Max(0.0, value);
            double roll = v - Math.Floor(v);
            for (int k = 0; k < digits.Length; k++)
            {
                digits[k] = (int)(Math.Floor(v / Math.Pow(10, k)) % 10);
                rolls[k] = roll;
                if (digits[k] != 9)
                {
                    roll = 0.0;
                }
            }
        }

        // Digits never round up, like a real odometer.
        internal static string FormatOdometer(double km, bool miles, bool trip)
        {
            double distance = Math.Max(0.0, miles ? km * MilesPerKm : km);
            if (trip)
            {
                return (Math.Floor(distance % 1000.0 * 10.0) / 10.0).ToString("000.0", CultureInfo.InvariantCulture);
            }
            return ((long)Math.Floor(distance) % 1000000L).ToString("000000", CultureInfo.InvariantCulture);
        }
    }
}
