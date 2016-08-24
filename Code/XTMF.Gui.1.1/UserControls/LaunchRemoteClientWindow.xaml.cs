﻿/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for LaunchRemoteClientWindow.xaml
    /// </summary>
    public partial class LaunchRemoteClientWindow : UserControl
    {
        internal Action<object> RequestClose;

        public LaunchRemoteClientWindow()
        {
            InitializeComponent();
            Loaded += LaunchRemoteClientWindow_Loaded;
        }

        private void LaunchRemoteClientWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Keyboard.Focus(Server);
                Server.Focus();
            }));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (e.Key == Key.W && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    var ev = RequestClose;
                    if (ev != null)
                    {
                        ev(this);
                        e.Handled = true;
                    }
                }
            }
            base.OnKeyDown(e);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Focus();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if (e.Source == this)
            {
                Keyboard.Focus(Server);
                Server.Focus();
                e.Handled = true;
            }
            base.OnGotFocus(e);
        }

        private void Launch_Clicked(object obj)
        {
            var address = Server.Text;
            var port = Port.Text;
            Process.Start(System.IO.Path.Combine(GetXTMFDirectory(), "XTMF.RemoteClient.exe"), AddQuotes(address) + " " + port);
        }

        private string GetXTMFDirectory()
        {
            return  System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private string AddQuotes(string address)
        {
            return string.Concat("\"", address, "\"");
        }

        private void Port_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(!e.Handled)
            {
                if(e.Key == Key.Enter)
                {
                    e.Handled = true;
                    if (!String.IsNullOrEmpty(Server.Text) && !String.IsNullOrEmpty(Port.Text))
                    {
                        Launch_Clicked(null);
                    }
                    else if (String.IsNullOrEmpty(Server.Text))
                    {
                        Keyboard.Focus(Server);
                    }
                }
            }
        }

        private void Server_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    if (!String.IsNullOrEmpty(Server.Text) && !String.IsNullOrEmpty(Port.Text))
                    {
                        Launch_Clicked(null);
                    }
                    else if(String.IsNullOrEmpty(Port.Text))
                    {
                        Keyboard.Focus(Port);
                    }
                }
            }
        }
    }
}
