﻿////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

  public class CLangDebuggeeThread : DebuggeeThread
  {

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public CLangDebuggeeThread (CLangDebuggeeProgram program, uint id)
      : base (program.DebugProgram, id)
    {
      NativeProgram = program;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public CLangDebuggeeProgram NativeProgram { get; protected set; }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public override void StackTrace ()
    {
      // 
      // Each thread maintains an internal cache of the last reported stacktrace. This is only cleared when threads are resumed via 'SetRunning(true)'.
      // 

      LoggingUtils.PrintFunction ();

      try
      {
        if (m_threadStackFrames.Count == 0)
        {
          NativeProgram.SelectThread (this);

          MiResultRecord resultRecord = m_debugProgram.AttachedEngine.NativeDebugger.GdbClient.SendCommand ("-stack-list-frames");

          if ((resultRecord != null) && (!resultRecord.IsError ()) && (resultRecord.HasField ("stack")))
          {
            MiResultValue stackRecord = resultRecord ["stack"];

            for (int i = 0; i < stackRecord.Count; ++i)
            {
              MiResultValueTuple frameTuple = stackRecord [i] as MiResultValueTuple;

              CLangDebuggeeStackFrame stackFrame = new CLangDebuggeeStackFrame (m_debugProgram.AttachedEngine.NativeDebugger, this, frameTuple);

              lock (m_threadStackFrames)
              {
                m_threadStackFrames.Add (stackFrame);
              }
            }
          }
        }
      }
      catch (Exception e)
      {
        LoggingUtils.HandleException (e);

        throw;
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
