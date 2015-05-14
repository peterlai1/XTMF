﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TMG.Input;
using Datastructure;
using TMG;
using TMG.DataUtility;
using Tasha.Common;
using XTMF;
using Tasha.XTMFModeChoice;



namespace Tasha.Validation.PerformanceMeasures
{
    public class AccessibilityCalculations : ISelfContainedModule
    {
        [RootModule]
        public ITashaRuntime Root;

        [SubModelInformation(Required = true, Description = "File containing the employment data")]
        public IResource EmploymentData;

        [RunParameter("Zones to Analyze", "1-1000", typeof(RangeSet), "The zones that you want to do the accessibility calculations for")]
        public RangeSet ZoneRange;

        [SubModelInformation(Required = true, Description = "The auto time matrix")]
        public IResource AutoTimeMatrix;

        [SubModelInformation(Required = true, Description = "The transit IVTT matrix")]
        public IResource TransitIVTTMatrix;

        [SubModelInformation(Required = true, Description = "The transit IVTT matrix")]
        public IResource TotalTransitTimeMatrix;

        [RunParameter("Accessibility Times to Analyze", "10", typeof(NumberList), "A comma separated list of accessibility times to execute this against.")]
        public NumberList AccessibilityTimes;

        [SubModelInformation(Required = true, Description = "Results file in .CSV format ")]
        public FileLocation ResultsFile;

        Dictionary<int, float> AutoAccessibilityResults = new Dictionary<int, float>();
        Dictionary<int, float> TransitAccessibilityResults = new Dictionary<int, float>();

        public void Start()
        {                        
            var employmentByZone = EmploymentData.AquireResource<SparseArray<float>>().GetFlatData();
            var popByZone = Root.ZoneSystem.ZoneArray.GetFlatData().Select(z => z.Population).ToArray();
            var AutoTimes = AutoTimeMatrix.AquireResource<SparseTwinIndex<float>>().GetFlatData();
            var TransitTimes = TransitIVTTMatrix.AquireResource<SparseTwinIndex<float>>().GetFlatData();

            int[] analyzedZonePopulation = (from z in Root.ZoneSystem.ZoneArray.GetFlatData()
                                           where ZoneRange.Contains(z.ZoneNumber)
                                           select z.ZoneNumber).ToArray();
                       
            float accessiblePopulation;

            foreach (var accessTime in AccessibilityTimes)
            {
                for (int i = 0; i < analyzedZonePopulation.Length; i++)
                {
                    for (int j = 0; j < employmentByZone.Length; j++)
                    {
                        if (AutoTimes[i][j] < accessTime)
                        {
                            accessiblePopulation = (analyzedZonePopulation[i] * employmentByZone[j]);
                            AddToResults(accessiblePopulation, accessTime, AutoAccessibilityResults);                            
                        }
                        if(TransitTimes[i][j] < accessTime)
                        {
                            accessiblePopulation = analyzedZonePopulation[i] * employmentByZone[j];
                            AddToResults(accessiblePopulation, accessTime, TransitAccessibilityResults); 
                        }
                    }
                }
            }                        

            using(StreamWriter writer = new StreamWriter(ResultsFile))
            {
                writer.WriteLine("Auto Accessibility");
                writer.WriteLine("Time(mins), Percentage Accessible");
                foreach(var pair in AutoAccessibilityResults)
                 {
                    var percentageAccessible = AutoAccessibilityResults[pair.Key] / (analyzedZonePopulation.Sum() * employmentByZone.Sum());
                    writer.WriteLine("{0},{1}", pair.Key, percentageAccessible);
                }

                writer.WriteLine("Transit Accessibility");
                writer.WriteLine("Time(mins), Percentage Accessible");
                foreach (var pair in TransitAccessibilityResults)
                {
                    var percentageAccessible = TransitAccessibilityResults[pair.Key] / (analyzedZonePopulation.Sum() * employmentByZone.Sum());
                    writer.WriteLine("{0},{1}", pair.Key, percentageAccessible);
                }
            }
        }


        public void AddToResults(float population, int accessTime, Dictionary<int, float> results)
        {
            if(results.ContainsKey(accessTime))
            {
                results[accessTime] += population;
            }
            else 
            {
                results.Add(accessTime, population);
            }
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(120, 25, 100); }
        }

        public bool RuntimeValidation(ref string error)
        {
            if (!EmploymentData.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the ODEmployment was not of type SparseArray<float>!";
                return false;
            }

            else if (!AutoTimeMatrix.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the AutoTimeMatrix was not of type SparseTwinIndex<float>!";
                return false;
            }

            else if (!TransitIVTTMatrix.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the AutoTimeMatrix was not of type SparseTwinIndex<float>!";
                return false;
            }

            return true;  
        }
    }
}