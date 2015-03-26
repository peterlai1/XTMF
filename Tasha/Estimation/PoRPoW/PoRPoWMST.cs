﻿/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datastructure;
using XTMF;
using TMG;
using TMG.Estimation;
using System.Threading.Tasks;
using TMG.Input;
using TMG.Functions;
using TMG.Functions;
using System.IO;

namespace Tasha.Estimation.PoRPoW
{
    public class PoRPoWMST : ITravelDemandModel, IResourceSource
    {

        [RootModule]
        public IEstimationClientModelSystem Root;

        [RunParameter("Input Directory", "../../Input", typeof(string), "The directory where the input is located.")]
        public string InputBaseDirectory { get; set; }

        public string Name { get; set; }

        [SubModelInformation(Required = false, Description = "The network data used by the model")]
        public IList<INetworkData> NetworkData { get; set; }

        [SubModelInformation(Description = "Modules to execute after running.")]
        public ISelfContainedModule[] PostRun;

        [SubModelInformation(Required = false, Description = "Optional: The location to save the model's results.")]
        public FileLocation ModelSaveFile;

        public string OutputBaseDirectory { get; set; }

        [RunParameter("Aggregate to planning district", false, "Should we aggregate to the planning district level (true) or zonal level?")]
        public bool AggregateToPlanningDistricts;

        [SubModelInformation(Description = "Distance Histogram")]
        public PoRPoWDistanceHistogram DistanceHistogram;

        public float Progress
        {
            get
            {
                return 0f;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return new Tuple<byte, byte, byte>(50, 150, 50);
            }
        }

        [SubModelInformation(Required = true, Description = "The zone system for the model")]
        public IZoneSystem ZoneSystem { get; set; }

        [SubModelInformation(Required = false, Description = "The resources for this model system")]
        public List<IResource> Resources { get; set; }

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            if(!TruthData.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the Truth Data is not of type SparseTwinIndex<float>!";
                return false;
            }
            if(!ModelData.CheckResourceType<SparseTriIndex<float>>())
            {
                error = "In '" + Name + "' the Model Data is not of type SparseTriIndex<float>!";
                return false;
            }
            return true;
        }

        [SubModelInformation(Required = true, Description = "The truth matrix to test against.")]
        public IResource TruthData;

        [SubModelInformation(Required = true, Description = "The model to test against the truth data.")]
        public IResource ModelData;

        [RunParameter("Reload Truth", false, "Reload the truth data each iteration.  Set this to true for batch runs.")]
        public bool LoadTruthEveryTime;

        bool FirstTime = true;

        private float InverseOfTotalTrips;
        private float[] TruthRows;
        private int[] ZoneToPDIndexMap;
        private SparseTwinIndex<float> PDError;
        private float[][] truth;

        public void Start()
        {
            if(FirstTime || LoadTruthEveryTime)
            {
                ZoneSystem.LoadData();
                foreach(var network in NetworkData)
                {
                    network.LoadData();
                }
                truth = TruthData.AquireResource<SparseTwinIndex<float>>().GetFlatData();
                TruthData.ReleaseResource();
            }
            var model = ModelData.AquireResource<SparseTriIndex<float>>().GetFlatData();
            var zones = ZoneSystem.ZoneArray.GetFlatData();
            // sum up the truth
            if(FirstTime || LoadTruthEveryTime)
            {
                // we only need to do this once
                TruthRows = (from row in truth
                             select row.Sum()).ToArray();

                InverseOfTotalTrips = 1.0f / TruthRows.Sum();
                if(AggregateToPlanningDistricts)
                {
                    PDError = ZoneSystemHelper.CreatePDTwinArray<float>(ZoneSystem.ZoneArray);
                    ZoneToPDIndexMap = (from zone in zones
                                        select PDError.GetFlatIndex(zone.PlanningDistrict)).ToArray();
                    // transform the truth to be PD based
                    truth = AggregateResults((new float[][][] { truth }), zones)
                        .Select(row => row.Select(element => element).ToArray()).ToArray();
                }
                FirstTime = false;
            }
            var aggregated = AggregateResults(model, zones);
            // calculate the error
            float error = ComputeError(truth, aggregated);
            // set the value in the root
            Root.RetrieveValue = () => error;
            if(ModelSaveFile != null)
            {
                SaveData.SaveMatrix(zones, AggregateResults(model, zones), ModelSaveFile);
            }

            if(this.DistanceHistogram != null)
            {
                var distances = ZoneSystem.Distances.GetFlatData();
                this.DistanceHistogram.Export(distances, model);
            }


            ModelData.ReleaseResource();
            for(int i = 0; i < PostRun.Length; i++)
            {
                PostRun[i].Start();
            }
        }

        [RunParameter("Use RMSE", false, "Use Root Mean Square Error instead of log likelihood.")]
        public bool UseRMSE;

        private float ComputeError(float[][] truth, float[][] aggregated)
        {
            float error = 0.0f;
            Parallel.For(0, truth.Length, () =>
            {
                // we start with no error
                return 0.0f;
            }, (int i, ParallelLoopState state, float localError) =>
            {
                var currentError = 0.0;
                var truthRow = truth[i];
                var sumOfRow = TruthRows[i];
                var aggRow = aggregated[i];
                if(sumOfRow > 0)
                {
                    if(UseRMSE)
                    {
                        // for each destination
                        if(VectorHelper.IsHardwareAccelerated)
                        {
                            currentError = VectorHelper.VectorSquareDiff(aggRow, 0, truthRow, 0, aggRow.Length);
                        }
                        else
                        {
                            for(int j = 0; j < truthRow.Length; j++)
                            {
                                var delta = aggRow[j] - truthRow[j];
                                currentError += delta * delta;
                            }
                        }
                    }
                    else
                    {
                        // for each destination
                        for(int j = 0; j < truthRow.Length; j++)
                        {
                            var pTruth = (truthRow[j] * InverseOfTotalTrips);
                            if(pTruth > 0)
                            {
                                var pModel = aggRow[j] * InverseOfTotalTrips;
                                double cellError;
                                if(pModel > pTruth)
                                {
                                    // y - deltaXY <=> 2y-x
                                    cellError = pTruth * Math.Log(Math.Min((Math.Max((pTruth + pTruth - pModel), 0)
                                        + (pTruth * 0.00015)) / (pTruth * 1.00015), 1));
                                }
                                else
                                {
                                    cellError = pTruth * Math.Log(Math.Min((pModel + (pTruth * 0.00015)) / (pTruth * 1.00015), 1));
                                }
                                currentError += cellError;
                            }
                        }
                    }
                }
                // the local error becomes the error for this zone plus the error that we have seen before
                return localError + (float)currentError;
            },
            (float localError) =>
            {
                // add the local error to the total error
                lock (this)
                {
                    error += localError;
                }
            });
            return error;
        }

        private float[][] AggArray;

        private float[][] AggregateResults(float[][][] model, IZone[] zones)
        {

            var ret = AggArray;
            if(ret == null)
            {
                ret = new float[zones.Length][];
                for(int i = 0; i < ret.Length; i++)
                {
                    ret[i] = new float[zones.Length];
                }
                AggArray = ret;
            }
            Parallel.For(0, zones.Length, (int i) =>
                {
                    var retRow = ret[i];
                    if(model.Length > 0)
                    {
                        var row = model[0][i];
                        for(int j = 0; j < row.Length; j++)
                        {
                            retRow[j] = row[j];
                        }
                        for(int k = 1; k < model.Length; k++)
                        {
                            row = model[k][i];
                            // accelerated
                            if(VectorHelper.IsHardwareAccelerated)
                            {
                                VectorHelper.VectorAdd(retRow, 0, retRow, 0, row, 0, row.Length);
                            }
                            else
                            {
                                for(int j = 0; j < row.Length; j++)
                                {
                                    retRow[j] += row[j];
                                }
                            }
                        }
                    }
                });
            if(AggregateToPlanningDistricts)
            {
                var data = PDError.GetFlatData();
                for(int i = 0; i < data.Length; i++)
                {
                    var row = data[i];
                    for(int j = 0; j < row.Length; j++)
                    {
                        row[j] = 0;
                    }
                }
                for(int i = 0; i < ret.Length; i++)
                {
                    var row = ret[i];
                    for(int j = 0; j < row.Length; j++)
                    {
                        data[ZoneToPDIndexMap[i]][ZoneToPDIndexMap[j]] += row[j];
                    }
                }
                return data;
            }
            return ret;
        }

        private static float SumModel(float[][][] model, int i, int j)
        {
            var total = 0.0f;
            for(int workerCategory = 0; workerCategory < model.Length; workerCategory++)
            {
                total += model[workerCategory][i][j];
            }
            return total;
        }

        public static float SumModelRow(float[][][] model, int i)
        {
            var total = 0.0f;
            for(int workerCategory = 0; workerCategory < model.Length; workerCategory++)
            {
                total += model[workerCategory][i].Sum();
            }
            return total;
        }

        public class PoRPoWDistanceHistogram : IModule
        {
            [RootModule]
            public PoRPoWMST Root;

            [RunParameter("Bins", "0-5;5-10;10-15;15-20;20-30;", typeof(RangeSet), "")]
            public RangeSet HistogramBins;

            [SubModelInformation(Description = "Save file location", Required = true)]
            public FileLocation SaveFile;

            [RunParameter("Coordinate Factor", 0.001f, "Convert from coordinate units to length units. For example, enter 0.001 to convert from coordinate meters to km.")]
            public float CoordinateFactor;

            internal void Export(float[][] distances, float[][][] model)
            {
                //var odMatrixData = model[this.WorkerCategory];

                float[][] binData = new float[1 + this.HistogramBins.Count][]; //Extra bin for outside of the array
                for(int i = 0; i < binData.Length; i++) binData[i] = new float[model.Length];

                for(int wcat = 0; wcat < model.Length; wcat++)
                {
                    var odMatrixData = model[wcat];

                    Parallel.For(0, odMatrixData.Length, (int i) =>
                    {
                        var row = odMatrixData[i];
                        var distanceRow = distances[i];

                        for(int j = 0; j < row.Length; j++)
                        {
                            var distance = (int)(distanceRow[j] * this.CoordinateFactor);

                            int index = this.HistogramBins.IndexOf(distance);

                            if(index < 0)
                            {
                                index = this.HistogramBins.Count; //The last index
                            }

                            binData[index][wcat] += row[j];
                        }
                    });
                }



                using (var writer = new StreamWriter(this.SaveFile.GetFilePath()))
                {
                    var header = "Distance";
                    foreach(var wcat in model) header += ",WCAT " + wcat;
                    writer.WriteLine(header);

                    for(int i = 0; i < this.HistogramBins.Count; i++)
                    {
                        var range = this.HistogramBins[i];
                        var binrow = binData[i];

                        var line = range.ToString();
                        foreach(var count in binrow) line += "," + count;

                        writer.WriteLine(line);
                    }

                    var lastline = this.HistogramBins[this.HistogramBins.Count - 1].Start + "+";
                    var lastbin = binData[this.HistogramBins.Count - 1];
                    foreach(var count in lastbin) lastline += "," + count;

                    writer.WriteLine(lastline);

                }

                Console.WriteLine("Exported PoRPoW histogram to " + this.SaveFile.GetFilePath());
            }

            private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(50, 150, 50);

            public string Name
            {
                get; set;
            }

            public float Progress
            {
                get;
                private set;
            }

            public Tuple<byte, byte, byte> ProgressColour
            {
                get
                {
                    return _ProgressColour;
                }
            }

            public bool RuntimeValidation(ref string error)
            {
                if(this.HistogramBins.Count < 1)
                {
                    error = "Histogram bins must define at least one range.";
                    return false;
                }

                return true;
            }


        }
    }
}
