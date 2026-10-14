// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace GalaxyExplorer
{
    public class OrbitUpdater : MonoBehaviour
    {
        /// <summary>Semi-major axis, km / DistanceScaleFactor.</summary>
        public float SemiMajorAxis;
        public float SemiMajorAxisReal;

        public float CurrentSemiMajorAxis
        {
            get
            {
                return Mathf.Lerp(SemiMajorAxis, SemiMajorAxisReal * RealitySemiMajorAxisScale, Reality);
            }
        }

        public float Eccentricity;
        public float EccentricityReal;

        private float CurrentEccentricity
        {
            get
            {
                return Mathf.Lerp(Eccentricity, EccentricityReal, Reality);
            }
        }

        /// <summary>Radians.</summary>
        public float ArgumentOfPerigee;

        /// <summary>Radians.</summary>
        public float Inclination;
        public float InclinationReal;

        private float CurrentInclination
        {
            get
            {
                return Mathf.Lerp(Inclination, InclinationReal, Reality);
            }
        }

        /// <summary>Radians.</summary>
        public float RightAscensionOfAscendingNode;

        /// <summary>Mean anomaly at <see cref="Epoch"/>, radians.</summary>
        public float MeanAnomaly;

        /// <summary>Sidereal period, days.</summary>
        public float Period;
        public float PeriodReal;

        public float CurrentPeriod
        {
            get
            {
                return Mathf.Lerp(Period, PeriodReal, Reality);
            }
        }

        public Coordinates Plane;

        /// <summary>Radians; used when the Newton-Raphson solve fails.</summary>
        public float TrueAnomaly;

        /// <summary>Julian date the elements are given at.</summary>
        public float Epoch;

        /// <summary>Blends each element from its schematic value (0) to its real one (1).</summary>
        [Range(0, 1)]
        public float Reality;

        public float RealitySemiMajorAxisScale = 1;

        public float Speed = 100.0f;
        public float SpeedMultiplier = 1;
        public float TransitionSpeedMultiplier = 1.0f;

        public int MaxIterations = 50;

        private DateTime myDateTime;

        public DateTime StartDate { get; private set; }

        private bool computed = false;

        private void Start()
        {
            StartDate = myDateTime = DateTime.Now;
        }

        private void Update()
        {
            if (computed && TransitionSpeedMultiplier == 0.0f)
            {
                // Frozen during transitions, but only once a first position has been set.
                return;
            }

            myDateTime += TimeSpan.FromDays(Time.deltaTime * Speed * SpeedMultiplier * TransitionSpeedMultiplier);
            transform.localPosition = CalculatePosition(myDateTime);

            computed = true;
        }

        public Vector3 CalculatePosition(DateTime time)
        {
            float trueAnomoly = CalculateTrueAnomaly(time);
            return CalculatePosition(trueAnomoly);
        }

        /// <summary>True anomaly by Newton-Raphson; falls back to <see cref="TrueAnomaly"/> if it does not converge.</summary>
        public float CalculateTrueAnomaly(DateTime dateTime)
        {
            const float epsilon = 0.000001f;
            float trueAnomoly;

            float meanAnomoly = CalculateMeanAnomaly(dateTime);

            float eccentricAnomoly, oldEccentricAnomoly, newEccentricAnomoly = meanAnomoly + (CurrentEccentricity / 2);

            // Solve [ 0 = E - e sin(E) - M ] for E using Newton-Raphson: Xn+1 = Xn - [ f(Xn) / f'(Xn) ]
            // E = Eccentric Anomaly, M = Mean Anomaly
            int iterations = 0;
            do
            {
                iterations++;
                oldEccentricAnomoly = newEccentricAnomoly;
                newEccentricAnomoly = oldEccentricAnomoly - (oldEccentricAnomoly - (CurrentEccentricity * Mathf.Sin(oldEccentricAnomoly)) - meanAnomoly) / (1.0f - (CurrentEccentricity * Mathf.Cos(oldEccentricAnomoly)));
            }
            while (Mathf.Abs(oldEccentricAnomoly - newEccentricAnomoly) > epsilon && iterations < MaxIterations);

            if (iterations == MaxIterations)
            {
                trueAnomoly = TrueAnomaly;
            }
            else
            {
                eccentricAnomoly = newEccentricAnomoly;
                float cosEccentricAnomoly = Mathf.Cos(eccentricAnomoly);

                // Solve cos(bodyAngle) = ( cos(E) - e ) / (1 - e cos(E) ) to get body angle with sun
                // E = Eccentric Anomaly, e = Eccentricity
                trueAnomoly = Mathf.Acos((cosEccentricAnomoly - CurrentEccentricity) / (1.0f - (CurrentEccentricity * cosEccentricAnomoly)));

                // Arccos returns value between 0 and Pi, but when Mean Anomoly > Pi (ie past halfway point) take (TwoPi - angle) to get solution between Pi and TwoPi
                if (meanAnomoly > Mathf.PI)
                {
                    trueAnomoly = (Mathf.PI * 2.0f) - trueAnomoly;
                }

                // Rounding can push the Acos argument just outside [-1, 1].
                if (float.IsNaN(trueAnomoly))
                {
                    trueAnomoly = TrueAnomaly;
                }
            }

            return trueAnomoly;
        }

        /// <summary>Mean anomaly, radians (0 to TwoPi).</summary>
        public float CalculateMeanAnomaly(DateTime dateTime)
        {
            float angle = (ElapsedDays(dateTime, Epoch) % CurrentPeriod) / CurrentPeriod * (Mathf.PI * 2) * (-1);

            angle += MeanAnomaly;

            if (angle > Mathf.PI * 2)
            {
                angle -= Mathf.PI * 2;
            }
            else if (angle < 0)
            {
                angle += Mathf.PI * 2;
            }

            return angle;
        }

        /// <summary>Position relative to the orbit origin for a true anomaly in radians.</summary>
        public Vector3 CalculatePosition(float trueAnomaly)
        {
            float radius = CurrentSemiMajorAxis * (1 - CurrentEccentricity * CurrentEccentricity) / (1 + CurrentEccentricity * Mathf.Cos(trueAnomaly));

            // XZ-plane is ecliptic, Y towards celestial north pole.
            return new Vector3(
                radius * (Mathf.Cos(RightAscensionOfAscendingNode) * Mathf.Cos(trueAnomaly + ArgumentOfPerigee) - Mathf.Sin(RightAscensionOfAscendingNode) * Mathf.Sin(trueAnomaly + ArgumentOfPerigee) * Mathf.Cos(CurrentInclination)),
                radius * (Mathf.Sin(trueAnomaly + ArgumentOfPerigee) * Mathf.Sin(CurrentInclination)),
                -radius * (Mathf.Sin(RightAscensionOfAscendingNode) * Mathf.Cos(trueAnomaly + ArgumentOfPerigee) + Mathf.Cos(RightAscensionOfAscendingNode) * Mathf.Sin(trueAnomaly + ArgumentOfPerigee) * Mathf.Cos(CurrentInclination)));
        }

        /// <summary>Days from <paramref name="epoch"/> (a Julian date) to <paramref name="dateTime"/>.</summary>
        public float ElapsedDays(DateTime dateTime, float epoch)
        {
            return ToJulianDate(dateTime) - epoch;
        }

        public float ToJulianDate(DateTime dateTime)
        {
            return ToJulianDay(dateTime) + (dateTime.Hour - 12) / 24f + dateTime.Minute / 1440f + dateTime.Second / 86400f;
        }

        public long ToJulianDay(DateTime dateTime)
        {
            int month = dateTime.Month;
            int day = dateTime.Day;
            int year = dateTime.Year;
            if (month < 3)
            {
                month += 12;
                year -= 1;
            }

            return day + (153 * month - 457) / 5 + 365 * year + (year / 4) - (year / 100) + (year / 400) + 1721119;
        }

        public enum Coordinates
        {
            Ecliptic,
            Equatorial,
            Galactic,
            Laplace
        }
    }
}