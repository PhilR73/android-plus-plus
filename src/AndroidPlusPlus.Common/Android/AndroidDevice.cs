﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace AndroidPlusPlus.Common
{
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  public class AndroidDevice
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private Hashtable m_deviceProperties = new Hashtable ();

    private List<AndroidProcess> m_deviceProcesses = new List<AndroidProcess> ();

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public AndroidDevice (string deviceId)
    {
      ID = deviceId;

      PopulateProperties ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void Refresh ()
    {
      LoggingUtils.PrintFunction ();

      PopulateProcesses ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public string GetProperty (string property)
    {
      return (string)m_deviceProperties [property];
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public AndroidProcess [] GetProcesses ()
    {
      return m_deviceProcesses.ToArray ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public string Shell (string command, string arguments, int timeout = 30000)
    {
      using (SyncRedirectProcess adbShellCommand = AndroidAdb.AdbCommand (this, "shell", string.Format ("{0} {1}", command, arguments)))
      {
        adbShellCommand.StartAndWaitForExit (timeout);

        LoggingUtils.Print ("[AndroidDevice] Shell (" + command + " " + arguments + "): " + adbShellCommand.StandardOutput);

        return adbShellCommand.StandardOutput;
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public bool Pull (string remotePath, string localPath)
    {
      using (SyncRedirectProcess adbPullCommand = AndroidAdb.AdbCommand (this, "pull", string.Format ("{0} {1}", remotePath, PathUtils.SantiseWindowsPath (localPath))))
      {
        adbPullCommand.StartAndWaitForExit (60000);

        LoggingUtils.Print ("[AndroidDevice] Pull: " + adbPullCommand.StandardOutput);

        if (adbPullCommand.StandardOutput.ToLower ().Contains ("not found"))
        {
          return false;
        }
      }

      return true;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public bool Install (string filename, bool reinstall)
    {
      // 
      // Install applications to internal storage (-l). Apps in /mnt/asec/ and other locations cause 'run-as' to fail regarding permissions.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        string temporaryStorage = "/data/local/tmp";

        string temporaryStoredFile = temporaryStorage + "/" + Path.GetFileName (filename);

        using (SyncRedirectProcess adbPushToTemporaryCommand = AndroidAdb.AdbCommand (this, "push", string.Format ("{0} {1}", PathUtils.SantiseWindowsPath (filename), temporaryStorage)))
        {
          if (adbPushToTemporaryCommand.StartAndWaitForExit (120000) != 0)
          {
            throw new InvalidOperationException ("Failed transferring to temporary storage.");
          }
        }

        using (SyncRedirectProcess adbInstallCommand = AndroidAdb.AdbCommand (this, "shell", string.Format ("pm install -f {0} {1}", ((reinstall) ? "-r " : ""), temporaryStoredFile)))
        {
          if (adbInstallCommand.StartAndWaitForExit (120000) != 0)
          {
            throw new InvalidOperationException ("Failed to install from temporary storage.");
          }

          if (!adbInstallCommand.StandardOutput.ToLower ().Contains ("success"))
          {
            throw new InvalidOperationException ("Failed to install from temporary storage. (" + adbInstallCommand.StandardOutput + ")");
          }
        }

        using (SyncRedirectProcess adbClearTemporary = AndroidAdb.AdbCommand (this, "shell", "rm " + temporaryStoredFile))
        {
          if (adbClearTemporary.StartAndWaitForExit (1000) != 0)
          {
            throw new InvalidOperationException ("Failed clearing temporary files.");
          }
        }

        LoggingUtils.Print ("[AndroidDevice] " + filename + " installed successfully.");

        return true;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);
      }

      LoggingUtils.Print ("[AndroidDevice] " + filename + " installation failed.");

      return false;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public bool Uninstall (string package, bool keepCache)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        using (SyncRedirectProcess adbUninstallCommand = AndroidAdb.AdbCommand (this, "install", ((keepCache) ? "-k " : "") + package))
        {
          if (adbUninstallCommand.StartAndWaitForExit (60000) != 0)
          {
            throw new InvalidOperationException ("Failed to uninstall '"+ package + "'.");
          }

          LoggingUtils.Print ("[AndroidDevice] Uninstall: " + adbUninstallCommand.StandardOutput);

          if (adbUninstallCommand.StandardOutput.ToLower ().Contains ("success"))
          {
            return true;
          }
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);
      }

      return false;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public AsyncRedirectProcess Logcat (AsyncRedirectProcess.EventListener listener)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        if (listener == null)
        {
          throw new ArgumentNullException ("listener");
        }

        AsyncRedirectProcess adbLogcatCommand = AndroidAdb.AdbCommandAsync (this, "logcat", "");

        adbLogcatCommand.Listener = listener;

        adbLogcatCommand.Start ();

        return adbLogcatCommand;
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);
      }

      return null;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private void PopulateProperties ()
    {
      LoggingUtils.PrintFunction ();

      string deviceGetProperties = Shell ("getprop", "");

      if (!String.IsNullOrEmpty (deviceGetProperties))
      {
        string [] getPropOutputLines = deviceGetProperties.Replace ("\r", "").Split (new char [] { '\n' });

        m_deviceProperties.Clear ();

        foreach (string line in getPropOutputLines)
        {
          if (!String.IsNullOrEmpty (line))
          {
            string [] segments = line.Split (new char [] { ' ' });

            string propName = segments [0].Trim (new char [] { '[', ']', ':' });

            string propValue = segments [1].Trim (new char [] { '[', ']', ':' });

            if ((!String.IsNullOrEmpty (propName)) && (!String.IsNullOrEmpty (propValue)))
            {
              m_deviceProperties.Add (propName, propValue);
            }
          }
        }
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private void PopulateProcesses ()
    {
      // 
      // Skip the first line, and read in tab-seperated process data.
      // 

      LoggingUtils.PrintFunction ();

      string deviceProcessList = Shell ("ps", "");

      if (!String.IsNullOrEmpty (deviceProcessList))
      {
        string [] processesOutputLines = deviceProcessList.Replace ("\r", "").Split (new char [] { '\n' });

        string processesRegExPattern = @"(?<user>[^ ]+)[ ]*(?<pid>[0-9]+)[ ]*(?<ppid>[0-9]+)[ ]*(?<vsize>[0-9]+)[ ]*(?<rss>[0-9]+)[ ]*(?<wchan>[A-Za-z0-9]+)[ ]*(?<pc>[A-Za-z0-9]+)[ ]*(?<s>[^ ]+)[ ]*(?<name>[^\r\n]+)";

        Regex regExMatcher = new Regex (processesRegExPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        m_deviceProcesses.Clear ();

        for (uint i = 1; i < processesOutputLines.Length; ++i)
        {
          if (!String.IsNullOrEmpty (processesOutputLines [i]))
          {
            Match regExLineMatches = regExMatcher.Match (processesOutputLines [i]);

            string processUser = regExLineMatches.Result ("${user}");

            uint processId = uint.Parse (regExLineMatches.Result ("${pid}"));

            uint processPid = uint.Parse (regExLineMatches.Result ("${ppid}"));

            uint processVsize = uint.Parse (regExLineMatches.Result ("${vsize}"));

            uint processRss = uint.Parse (regExLineMatches.Result ("${rss}"));

            uint processWchan = Convert.ToUInt32 (regExLineMatches.Result ("${wchan}"), 16);

            uint processPc = Convert.ToUInt32 (regExLineMatches.Result ("${pc}"), 16);

            string processPcS = regExLineMatches.Result ("${s}");

            string processName = regExLineMatches.Result ("${name}");

            if ((!String.IsNullOrEmpty (processName)) && (!String.IsNullOrEmpty (processUser)))
            {
              AndroidProcess process = new AndroidProcess (this, processName, processId, processUser);

              m_deviceProcesses.Add (process);
            }
          }
        }
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public string ID { get; protected set; }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public AndroidSettings.VersionCode SdkVersion { get; protected set; }

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
