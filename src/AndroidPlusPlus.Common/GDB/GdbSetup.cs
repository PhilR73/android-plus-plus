﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace AndroidPlusPlus.Common
{

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  public sealed class GdbSetup : IDisposable
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public GdbSetup (AndroidProcess process, string [] libraryPaths)
    {
      Process = process;

      Socket = "debug-socket";

      Host = "localhost";

      Port = 5039;

      CacheDirectory = string.Format (@"{0}\AndroidMT\Cache\{1}\{2}", Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), Process.HostDevice.ID, Process.Name);

      Directory.CreateDirectory (CacheDirectory);

      // 
      // Evaluate a compound list of explicit native library locations on the host machine.
      // 

      LibraryPaths = new List<string> (libraryPaths);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void Dispose ()
    {
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public AndroidProcess Process { get; private set; }

    public string Socket { get; private set; }

    public string Host { get; private set; }

    public uint Port { get; private set; }

    public List <string> LibraryPaths { get; private set; }

    public string CacheDirectory { get; private set; }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void SetupPortForwarding ()
    {
      // 
      // Setup network redirection.
      // 

      Trace.WriteLine (string.Format ("[GdbSetup] Setup network redirection."));

      using (SyncRedirectProcess adbPortForward = AndroidAdb.AdbCommand (Process.HostDevice, "forward", string.Format ("tcp:{0} tcp:{1}", Port, Port)))
      {
        adbPortForward.StartAndWaitForExit (1000);
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void ClearPortForwarding ()
    {
      // 
      // Clear network redirection.
      // 

      Trace.WriteLine (string.Format ("[GdbSetup] Clear network redirection."));

      using (SyncRedirectProcess adbPortForward = AndroidAdb.AdbCommand (Process.HostDevice, "forward", "--remove-all"))
      {
        adbPortForward.StartAndWaitForExit (1000);
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public string [] CacheDeviceBinaries ()
    {
      // 
      // Pull the required binaries from the device.
      // 

      List<string> deviceBinaries = new List<string> ();

      Trace.WriteLine (string.Format ("[GdbSetup] CacheDeviceBinaries: "));

      if (Process.HostDevice.Pull ("/system/bin/app_process", Path.Combine (CacheDirectory, "app_process")))
      {
        Trace.WriteLine (string.Format ("[GdbSetup] Pulled app_process from device/emulator."));

        deviceBinaries.Add (Path.Combine (CacheDirectory, "app_process"));
      }

      if (Process.HostDevice.Pull ("/system/bin/linker", Path.Combine (CacheDirectory, "linker")))
      {
        Trace.WriteLine (string.Format ("[GdbSetup] Pulled linker from device/emulator."));

        deviceBinaries.Add (Path.Combine (CacheDirectory, "linker"));
      }

      if (Process.HostDevice.Pull ("/system/lib/libc.so", Path.Combine (CacheDirectory, "libc.so")))
      {
        Trace.WriteLine (string.Format ("[GdbSetup] Pulled libc.so from device/emulator."));

        deviceBinaries.Add (Path.Combine (CacheDirectory, "libc.so"));
      }

      if (Process.HostDevice.Pull (string.Format ("{0}/lib/", Process.InternalCacheDirectory), CacheDirectory))
      {
        Trace.WriteLine (string.Format ("[GdbSetup] Pulled application libraries from device/emulator."));

        string [] additionalLibraries = Directory.GetFiles (CacheDirectory, "lib*.so", SearchOption.AllDirectories);

        foreach (string lib in additionalLibraries)
        {
          if (!deviceBinaries.Contains (lib))
          {
            deviceBinaries.Add (lib);
          }
        }
      }

      return deviceBinaries.ToArray ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public string [] CreateGdbExecutionScript ()
    {
      List<string> gdbExecutionCommands = new List<string> ();

      gdbExecutionCommands.Add ("set target-async on");

      gdbExecutionCommands.Add ("set breakpoint pending on");

      //gdbExecutionCommands.Add ("set debug remote 1");

      //gdbExecutionCommands.Add ("set debug infrun 1");

      string libraryDirectories = string.Join (" ", LibraryPaths);

      gdbExecutionCommands.Add ("directory " + libraryDirectories);

      string appProcessPath = Path.Combine (CacheDirectory, "app_process");

      if (File.Exists (appProcessPath))
      {
        gdbExecutionCommands.Add ("file " + StringUtils.ConvertPathWindowsToPosix (appProcessPath));
      }

      return gdbExecutionCommands.ToArray ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  }

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////