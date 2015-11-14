﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.Debugger.Interop;
using AndroidPlusPlus.Common;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace AndroidPlusPlus.VsDebugEngine
{

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

  public class JavaLangDebugger : IDisposable
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public delegate void InterruptOperation (JavaLangDebugger debugger);

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private JdbSetup m_jdbSetup;

    private JavaLangDebuggerCallback m_javaLangCallback = null;

    private int m_interruptOperationCounter = 0;

    private ManualResetEvent m_interruptOperationCompleted = null;

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public JavaLangDebugger (DebugEngine debugEngine, DebuggeeProgram debugProgram)
    {
      Engine = debugEngine;

      m_javaLangCallback = new JavaLangDebuggerCallback (debugEngine);

      JavaProgram = new JavaLangDebuggeeProgram (this, debugProgram);

      m_jdbSetup = new JdbSetup (debugProgram.DebugProcess.NativeProcess);

      Engine.Broadcast (new DebugEngineEvent.DebuggerConnectionEvent (DebugEngineEvent.DebuggerConnectionEvent.EventType.LogStatus, string.Format ("Configuring JDB for {0}:{1}...", m_jdbSetup.Host, m_jdbSetup.Port)), null, null);

      JdbClient = new JdbClient (m_jdbSetup);

      JdbClient.OnAsyncStdout = OnClientAsyncOutput;

      JdbClient.OnAsyncStderr = OnClientAsyncOutput;

      JdbClient.Start ();
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void Kill ()
    {
      LoggingUtils.PrintFunction ();

      if (JdbClient != null)
      {
        JdbClient.Kill ();
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void Dispose ()
    {
      Dispose (true);

      GC.SuppressFinalize (this);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    protected virtual void Dispose (bool disposing)
    {
      if (disposing)
      {
        if (JdbClient != null)
        {
          JdbClient.Dispose ();

          JdbClient = null;
        }

        if (m_jdbSetup != null)
        {
          m_jdbSetup.Dispose ();

          m_jdbSetup = null;
        }
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public JdbClient JdbClient { get; protected set; }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public DebugEngine Engine { get; protected set; }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public JavaLangDebuggeeProgram JavaProgram { get; protected set; }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private void OnClientAsyncOutput (string [] output)
    {
      LoggingUtils.PrintFunction ();

      bool requestTermination = false;

      foreach (string line in output)
      {
        if (line.StartsWith ("Unable to attach to target VM."))
        {
          // 
          // Failed to connect to target. Usually because DDMS/Monitor is in use. Display an error message and close debug session.
          // 

          string message = "Unable to attach to target VM.\nThis is usually because there's already an active connection.\nPlease close any instances of DDMS/Monitor or JVM/JDB, and try again.";

          Engine.Broadcast (new DebugEngineEvent.Error (message, true), JavaProgram.DebugProgram, null);

          requestTermination = true;
        }
        else if (line.StartsWith ("The application has been disconnected"))
        {
          string message = "Lost connection with JDB target application.\nThe application has been disconnected.";

          Engine.Broadcast (new DebugEngineEvent.Error (message, true), JavaProgram.DebugProgram, null);
        }
        else if (line.StartsWith ("Exception occurred:"))
        {
          // 
          // Target exception handling. 
          // TODO: Just continuing execution until this is implemented properly.
          // 

          JdbClient.Continue ();
        }
      }

      if (requestTermination)
      {
        ThreadPool.QueueUserWorkItem (delegate (object state)
        {
          try
          {
            LoggingUtils.RequireOk (Engine.Detach (JavaProgram.DebugProgram));
          }
          catch (Exception e)
          {
            LoggingUtils.HandleException (e);
          }
        });
      }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public DebuggeeCodeContext GetCodeContextForLocation (string location)
    {
      LoggingUtils.PrintFunction ();

      try
      {
        if (string.IsNullOrEmpty (location))
        {
          throw new ArgumentNullException ("location");
        }

        if (location.StartsWith ("0x"))
        {
          location = "*" + location;
        }
        else if (location.StartsWith ("\""))
        {
          location = location.Replace ("\\", "/");

          location = location.Replace ("\"", "\\\""); // required to escape the nested string.
        }

        throw new NotImplementedException ();
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

  }

  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
  ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
