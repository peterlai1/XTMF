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
using TMG;
using XTMF;
using Datastructure;
using TMG.Functions;
using TMG.Functions;
namespace Tasha.Data
{
    [ModuleInformation(Description =
        @"This module is designed to add multiple rates for each zone.")]
    public class AddRates : IDataSource<SparseArray<float>>
    {
        private SparseArray<float> Data;

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation(Required = false, Description = "The resources to add together.")]
        public IResource[] ResourcesToAdd;

        [RunParameter("Save by PD", true, "Should we save our combined rate by PD?  If true then all rates are treated as if by PD!")]
        public bool SaveRatesBasedOnPD;

        public SparseArray<float> GiveData()
        {
            return Data;
        }

        public bool Loaded
        {
            get { return Data != null; }
        }

        public void LoadData()
        {
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            var resources = ResourcesToAdd.Select(resource => resource.AquireResource<SparseArray<float>>().GetFlatData()).ToArray();
            SparseArray<float> data;
            data = SaveRatesBasedOnPD ? ZoneSystemHelper.CreatePDArray<float>(zoneArray) : zoneArray.CreateSimilarArray<float>();
            var flatData = data.GetFlatData();
            if(VectorHelper.IsHardwareAccelerated)
            {
                for(int j = 0; j < resources.Length; j++)
                {
                    VectorHelper.VectorAdd(flatData, 0, flatData, 0, resources[j], 0, flatData.Length);
                }
            }
            else
            {
                for(int j = 0; j < resources.Length; j++)
                {
                    var currentResource = resources[j];
                    for(int i = 0; i < currentResource.Length; i++)
                    {
                        flatData[i] += currentResource[i];
                    }
                }
            }
            Data = data;
        }

        public void UnloadData()
        {
            Data = null;
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            for(int i = 0; i < ResourcesToAdd.Length; i++)
            {
                if(!ResourcesToAdd[i].CheckResourceType<SparseArray<float>>())
                {
                    error = "In '" + Name + "' the resource '" + ResourcesToAdd[i].Name + "' is not of type SparseArray<float>!";
                    return false;
                }
            }
            return true;
        }
    }
}
