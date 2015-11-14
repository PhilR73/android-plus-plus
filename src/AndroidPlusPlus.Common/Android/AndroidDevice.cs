﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;

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

    private Dictionary<string, string> m_deviceProperties = new Dictionary<string, string> ();

    private Dictionary<uint, AndroidProcess> m_deviceProcessesByPid = new Dictionary<uint, AndroidProcess> ();

    private Dictionary<string, HashSet<uint>> m_devicePidsByName = new Dictionary<string, HashSet<uint>> ();

    private Dictionary<uint, HashSet<uint>> m_devicePidsByPpid = new Dictionary<uint, HashSet<uint>> ();

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

    public bool IsEmulator
    {
      get
      {
        return ID.StartsWith ("emulator-");
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public bool IsOverWiFi
    {
      get
      {
        return ID.Contains (".");
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void Refresh ()
    {
      LoggingUtils.PrintFunction ();

      RefreshProcesses ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public string GetProperty (string property)
    {
      string prop;

      if (m_deviceProperties.TryGetValue (property, out prop))
      {
        return prop;
      }

      return string.Empty;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public AndroidProcess GetProcessFromPid (uint processId)
    {
      AndroidProcess process;

      if (m_deviceProcessesByPid.TryGetValue (processId, out process))
      {
        return process;
      }

      return null;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public uint [] GetPidsFromName (string processName)
    {
      HashSet<uint> processPidSet;

      uint [] processesArray = new uint [] { };

      if (m_devicePidsByName.TryGetValue (processName, out processPidSet))
      {
        processesArray = new uint [processPidSet.Count];

        processPidSet.CopyTo (processesArray, 0);
      }

      return processesArray;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public uint [] GetChildPidsFromPpid (uint parentProcessId)
    {
      HashSet<uint> processPpidSiblingSet;

      uint [] processesArray = new uint [] { };

      if (m_devicePidsByPpid.TryGetValue (parentProcessId, out processPpidSiblingSet))
      {
        processesArray = new uint [processPpidSiblingSet.Count];

        processPpidSiblingSet.CopyTo (processesArray, 0);
      }

      return processesArray;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public uint [] GetActivePids ()
    {
      uint [] activePids = new uint [m_deviceProcessesByPid.Count];

      m_deviceProcessesByPid.Keys.CopyTo (activePids, 0);

      return activePids;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public string Shell (string command, string arguments, int timeout = 30000)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        int exitCode = -1;

        using (SyncRedirectProcess process = AndroidAdb.AdbCommand (this, "shell", string.Format ("{0} {1}", command, arguments)))
        {
          exitCode = process.StartAndWaitForExit (timeout);

          if (exitCode != 0)
          {
            throw new InvalidOperationException (string.Format ("[shell:{0}] returned error code: {1}", command, exitCode));
          }

          return process.StandardOutput;
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        throw new InvalidOperationException (string.Format ("[shell:{0}] failed", command), e);
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public string Push (string localPath, string remotePath)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        int exitCode = -1;

        using (SyncRedirectProcess process = AndroidAdb.AdbCommand (this, "push", string.Format ("{0} {1}", PathUtils.QuoteIfNeeded (localPath), remotePath)))
        {
          exitCode = process.StartAndWaitForExit ();

          if (exitCode != 0)
          {
            throw new InvalidOperationException (string.Format ("[push] returned error code: {0}", exitCode));
          }

          return process.StandardOutput;
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        throw new InvalidOperationException ("[push] failed", e);
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public string Pull (string remotePath, string localPath)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        int exitCode = -1;

        using (SyncRedirectProcess process = AndroidAdb.AdbCommand (this, "pull", string.Format ("{0} {1}", remotePath, PathUtils.QuoteIfNeeded (localPath))))
        {
          exitCode = process.StartAndWaitForExit ();

          if (exitCode != 0)
          {
            throw new InvalidOperationException (string.Format ("[pull] returned error code: {0}", exitCode));
          }

          return process.StandardOutput;
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        throw new InvalidOperationException ("[pull] failed", e);
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private void PopulateProperties ()
    {
      LoggingUtils.PrintFunction ();

      string getPropOutput = Shell ("getprop", "");

      if (!string.IsNullOrEmpty (getPropOutput))
      {
        string pattern = @"^\[(?<key>[^\]:]+)\]:[ ]+\[(?<value>[^\]$]+)";

        Regex regExMatcher = new Regex (pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        string [] properties = getPropOutput.Replace ("\r", "").Split (new char [] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < properties.Length; ++i)
        {
          if (!properties [i].StartsWith ("["))
          {
            continue; // early rejection.
          }

          string unescapedStream = Regex.Unescape (properties [i]);

          Match regExLineMatch = regExMatcher.Match (unescapedStream);

          if (regExLineMatch.Success)
          {
            string key = regExLineMatch.Result ("${key}");

            string value = regExLineMatch.Result ("${value}");

            if (string.IsNullOrEmpty (key) || key.Equals ("${key}"))
            {
              continue;
            }
            else if (value.Equals ("${value}"))
            {
              continue;
            }
            else 
            {
              m_deviceProperties [key] = value;
            }
          }
        }
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void RefreshProcesses (uint processIdFilter = 0)
    {
      // 
      // Skip the first line, and read in tab-separated process data.
      // 

      LoggingUtils.PrintFunction ();

      string deviceProcessList = Shell ("ps", string.Format ("-t {0}", ((processIdFilter == 0) ? "" : processIdFilter.ToString ())));

      if (!String.IsNullOrEmpty (deviceProcessList))
      {
        string [] processesOutputLines = deviceProcessList.Replace ("\r", "").Split (new char [] { '\n' });

        string processesRegExPattern = @"(?<user>[^ ]+)[ ]*(?<pid>[0-9]+)[ ]*(?<ppid>[0-9]+)[ ]*(?<vsize>[0-9]+)[ ]*(?<rss>[0-9]+)[ ]*(?<wchan>[^ ]+)[ ]*(?<pc>[A-Za-z0-9]+)[ ]*(?<s>[^ ]+)[ ]*(?<name>[^\r\n]+)";

        Regex regExMatcher = new Regex (processesRegExPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        m_deviceProcessesByPid.Clear ();

        m_devicePidsByName.Clear ();

        m_devicePidsByPpid.Clear ();

        for (uint i = 1; i < processesOutputLines.Length; ++i)
        {
          if (!String.IsNullOrEmpty (processesOutputLines [i]))
          {
            Match regExLineMatches = regExMatcher.Match (processesOutputLines [i]);

            string processUser = regExLineMatches.Result ("${user}");

            uint processPid = uint.Parse (regExLineMatches.Result ("${pid}"));

            uint processPpid = uint.Parse (regExLineMatches.Result ("${ppid}"));

            uint processVsize = uint.Parse (regExLineMatches.Result ("${vsize}"));

            uint processRss = uint.Parse (regExLineMatches.Result ("${rss}"));

            string processWchan = regExLineMatches.Result ("${wchan}");

            string processPc = regExLineMatches.Result ("${pc}");

            string processPcS = regExLineMatches.Result ("${s}");

            string processName = regExLineMatches.Result ("${name}");

            AndroidProcess process = new AndroidProcess (this, processName, processPid, processPpid, processUser);

            m_deviceProcessesByPid [processPid] = process;

            // 
            // Add new process to a fast-lookup collection organised by process name.
            // 

            HashSet<uint> processPidsList;

            if (!m_devicePidsByName.TryGetValue (processName, out processPidsList))
            {
              processPidsList = new HashSet<uint> ();
            }

            if (!processPidsList.Contains (processPid))
            {
              processPidsList.Add (processPid);
            }

            m_devicePidsByName [processName] = processPidsList;

            // 
            // Check whether this process is sibling of another; keep ppids-pid relationships tracked.
            // 

            HashSet<uint> processPpidSiblingList;

            if (!m_devicePidsByPpid.TryGetValue (processPpid, out processPpidSiblingList))
            {
              processPpidSiblingList = new HashSet<uint> ();
            }

            if (!processPpidSiblingList.Contains (process.Pid))
            {
              processPpidSiblingList.Add (process.Pid);
            }

            m_devicePidsByPpid [processPpid] = processPpidSiblingList;
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

    public AndroidSettings.VersionCode SdkVersion 
    {
      get
      {
        // 
        // Query device's current SDK level. If it's not an integer (like some custom ROMs) fall-back to ICS.
        // 

        try
        {
          int sdkLevel = int.Parse (GetProperty ("ro.build.version.sdk"));

          return (AndroidSettings.VersionCode) sdkLevel;
        }
        catch (Exception e)
        {
          LoggingUtils.HandleException (e);

          return AndroidSettings.VersionCode.GINGERBREAD;
        }
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public string [] SupportedCpuAbis
    {
      get
      {
        // 
        // Queries device's supported CPU ABIs. Fallback to using old-style primary/secondary props if list isn't available.
        // 

        string abiList = GetProperty ("ro.product.cpu.abilist");

        if (!string.IsNullOrEmpty (abiList))
        {
          return abiList.Split (',');
        }

        return new string []
        {
          GetProperty ("ro.product.cpu.abi"),
          GetProperty ("ro.product.cpu.abi2")
        };
      }
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
