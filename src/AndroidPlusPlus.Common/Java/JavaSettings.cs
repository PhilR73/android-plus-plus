﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace AndroidPlusPlus.Common
{

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  public class JavaSettings
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public static string JdkRoot
    {
      get
      {

        // 
        // Probe for possible JDK installation directories.
        // 

        List<string> jdkPossibleLocations = new List<string> (2);

        try
        {
          jdkPossibleLocations.Add (Environment.GetEnvironmentVariable ("JAVA_HOME"));

          using (RegistryKey localMachineJavaDevelopmentKit = Registry.LocalMachine.OpenSubKey (@"SOFTWARE\JavaSoft\Java Development Kit\"))
          {
            if (localMachineJavaDevelopmentKit != null)
            {
              string currentVersion = localMachineJavaDevelopmentKit.GetValue ("CurrentVersion") as string;

              if (!string.IsNullOrEmpty (currentVersion))
              {
                using (RegistryKey localMachineJdkCurrentVersion = localMachineJavaDevelopmentKit.OpenSubKey (currentVersion))
                {
                  if (localMachineJdkCurrentVersion != null)
                  {
                    jdkPossibleLocations.Add (localMachineJdkCurrentVersion.GetValue ("JavaHome") as string);
                  }
                }
              }
            }
          }

          // 
          // Search specified path the default 'java.exe' executable.
          // 

          foreach (string location in jdkPossibleLocations)
          {
            if (!string.IsNullOrEmpty (location))
            {
              if (File.Exists (location + @"\bin\java.exe"))
              {
                return location;
              }
            }
          }
        }
        catch (Exception e)
        {
          LoggingUtils.HandleException (e);
        }

        return string.Empty;
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
