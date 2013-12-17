﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace AndroidPlusPlus.MsBuild.Exporter
{

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  class Program
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private static List<string> s_templateDirs = new List<string> ();

    private static List<string> s_vsVersions = new List<string> ();

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    static int Main (string [] args)
    {
      try
      {
        ProcessArguments (args);

        KillMsBuildInstances ();

        Dictionary<string, string> textSubstitution = new Dictionary<string, string> ();

        textSubstitution.Add ("{master}", "Android++");

        foreach (string version in s_vsVersions)
        {
          ExportMsBuildTemplateForVersion (version, ref textSubstitution);
        }
      }
      catch (Exception e)
      {
        string exception = string.Format ("[AndroidPlusPlus.MsBuild.Exporter] Exception: {0}\nStack trace:\n{1}", e.Message, e.StackTrace);

        Console.WriteLine (exception);

        Trace.WriteLine (exception);

        PrintArgumentUsage ();

        return 1;
      }

      Console.WriteLine ("[AndroidPlusPlus.MsBuild.Exporter] Success.");

      Trace.WriteLine ("[AndroidPlusPlus.MsBuild.Exporter] Success.");

      return 0;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private static void ProcessArguments (string [] args)
    {
      if ((args == null) || (args.Length == 0))
      {
        throw new ArgumentException ("No arguments specified");
      }

      for (int i = 0; i < args.Length; ++i)
      {
        switch (args [i])
        {
          case "--template-dir":
          {
            string template = args [++i].Replace ("\"", "");

            if (!Directory.Exists (template))
            {
              throw new DirectoryNotFoundException ("--template-dir references non-existent directory. Tried: " + template);
            }

            s_templateDirs.Add (template);

            break;
          }

          case "--vs-version":
          {
            string [] versions = args [++i].Split (';');

            if (versions.Length == 0)
            {
              throw new ArgumentException ("--vs-version not defined correctly.");
            }

            foreach (string version in versions)
            {
              switch (version)
              {
                case "2010":
                case "2012":
                case "2013":
                {
                  s_vsVersions.Add (version);

                  break;
                }

                default:
                {
                  throw new ArgumentException ("--vs-version references invalid version. Tried: " + version);
                }
              }
            }

            break;
          }
        }
      }

      // 
      // Validate the tool executed with appropriate arguments.
      // 

      if (s_templateDirs.Count () == 0)
      {
        throw new ArgumentException ("--template-dir not specified.");
      }

      if (s_templateDirs.Count () > 1)
      {
        throw new ArgumentException ("Please only specify a single target --template-dir.");
      }

      if (s_vsVersions.Count () == 0)
      {
        throw new ArgumentException ("--vs-version not specified.");
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private static void PrintArgumentUsage ()
    {
      // TODO
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private static void KillMsBuildInstances ()
    {
      Process [] activeMsBuildProcesses = Process.GetProcessesByName ("MSBuild");

      foreach (Process process in activeMsBuildProcesses)
      {
        process.Kill ();
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private static void ExportMsBuildTemplateForVersion (string version, ref Dictionary <string, string> textSubstitution)
    {
      string msBuildInstallationDir = string.Empty;

      switch (version)
      {
        case "2010":
        {
          // 
          // Lookup MSBuild platforms directory under 'Program Files' and 'Program Files (x86)'.
          // 

          msBuildInstallationDir = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles) + @"\MSBuild\Microsoft.Cpp\v4.0\";

          if (!Directory.Exists (msBuildInstallationDir))
          {
            msBuildInstallationDir = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86) + @"\MSBuild\Microsoft.Cpp\v4.0\";
          }

          if (!Directory.Exists (msBuildInstallationDir))
          {
            throw new DirectoryNotFoundException ("Could not locate required MSBuild platforms directory. This should have been installed with VS2010. Tried: " + msBuildInstallationDir);
          }

          break;
        }

        default:
        {
          throw new NotImplementedException ("Export procedure for '" + version + "' is not implemented");
        }
      }

      // 
      // Clean any existing MsBuild deployment.
      // 

      foreach (string directory in Directory.GetDirectories (msBuildInstallationDir))
      {
        if (directory.Contains (textSubstitution ["{master}"]))
        {
          Directory.Delete (directory, true);
        }
      }

      foreach (string directory in Directory.GetDirectories (msBuildInstallationDir + @"\Platforms"))
      {
        if (directory.Contains (textSubstitution ["{master}"]))
        {
          Directory.Delete (directory, true);
        }
      }

      // 
      // Copy each directory of the template directories and apply pattern processing.
      // 

      foreach (string templateDir in s_templateDirs)
      {
        Console.WriteLine (string.Format ("[AndroidPlusPlus.MsBuild.Exporter] Copying {0} to {1}", templateDir, msBuildInstallationDir));

        CopyFoldersAndFiles (templateDir, msBuildInstallationDir, true, ref textSubstitution);
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private static void CopyFoldersAndFiles (string sourcePath, string destinationPath, bool recursive, ref Dictionary<string, string> textSub)
    {
      if (Directory.Exists (sourcePath))
      {
        Directory.CreateDirectory (ApplyTextSubstitution (destinationPath.Replace (sourcePath, destinationPath), ref textSub));

        foreach (string directory in Directory.GetDirectories (sourcePath, "*", (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
        {
          Directory.CreateDirectory (ApplyTextSubstitution (directory.Replace (sourcePath, destinationPath), ref textSub));
        }

        foreach (string file in Directory.GetFiles (sourcePath, "*.*", (recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
        {
          string newFileName = ApplyTextSubstitution (file.Replace (sourcePath, destinationPath), ref textSub);

          File.Copy (file, newFileName, true);

          // 
          // Process file contents with same text subsitution settings too.
          // 

          if (Path.GetExtension (newFileName) != ".dll")
          {
            StringBuilder fileContents = new StringBuilder (File.ReadAllText (newFileName));

            foreach (KeyValuePair<string, string> keyPair in textSub)
            {
              fileContents.Replace (keyPair.Key, keyPair.Value);
            }

            File.WriteAllText (newFileName, fileContents.ToString ());
          }
        }
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private static string ApplyTextSubstitution (string original, ref Dictionary<string, string> textSub)
    {
      StringBuilder substitutedString = new StringBuilder (original);

      foreach (KeyValuePair<string, string> keyPair in textSub)
      {
        substitutedString.Replace (keyPair.Key, keyPair.Value);
      }

      return substitutedString.ToString ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  }

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

}