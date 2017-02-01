/*
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
using System.IO;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Analysis
{
    public class ValidateStudents : ISelfContainedModule
    {
        [RootModule]
        public IDemographicsModelSystemTemplate Root;

        [SubModelInformation( Required = true, Description = "Where to save the analysis. (CSV)" )]
        public FileLocation SaveTo;

        public void Start()
        {
            Progress = 0f;
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var ageRates = Root.Demographics.AgeRates.GetFlatData();
            var ageCategories = Root.Demographics.AgeCategories.GetFlatData();
            var studentRates = Root.Demographics.SchoolRates.GetFlatData();
            var employmentRates = Root.Demographics.EmploymentStatusRates.GetFlatData();
            var employmentCategories = Root.Demographics.EmploymentStatus.GetFlatData();
            using ( var writer = new StreamWriter( SaveTo.GetFilePath() ) )
            {
                writer.WriteLine( "Zone,AgeCategory,EmpStat,Persons" );
                for ( int i = 0; i < ageRates.Length; i++ )
                {
                    var ageRate = ageRates[i];
                    var studentRate = studentRates[i].GetFlatData();
                    var pop = zones[i].Population;
                    var zoneNumber = zones[i].ZoneNumber;
                    var empRate = employmentRates[i].GetFlatData();
                    for ( int age = 0; age < ageRate.Length; age++ )
                    {
                        var agePop = pop * ageRate[age];
                        var stuEmpRate = studentRate[age];
                        for ( int emp = 0; emp < stuEmpRate.Length; emp++ )
                        {
                            writer.Write( zoneNumber );
                            writer.Write( ',' );
                            writer.Write( ageCategories[age] );
                            writer.Write( ',' );
                            writer.Write( employmentCategories[emp] );
                            writer.Write( ',' );
                            writer.WriteLine( stuEmpRate[emp] * agePop * empRate[age][emp] );
                        }
                    }
                    // Update our progress
                    Progress = (float)i / zones.Length;
                }
            }
            Progress = 1f;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour => null;

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
