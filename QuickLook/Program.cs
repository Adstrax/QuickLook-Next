// Copyright © 2017-2026 QL-Win Contributors
//
// This file is part of QuickLook program.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;

namespace QuickLook;

public static class Program
{
    /// <summary>
    /// Application entry point. A second instance exists only to forward the
    /// requested path to the running instance; doing that before WPF starts
    /// skips the ~400 ms of PresentationFramework/XAML initialization every
    /// preview invocation used to pay. When the forwarder cannot reach a
    /// running instance, fall back to the normal WPF startup.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        if (StartupForwarder.TryForwardToRunningInstance())
            return;

        App.Main();
    }
}
