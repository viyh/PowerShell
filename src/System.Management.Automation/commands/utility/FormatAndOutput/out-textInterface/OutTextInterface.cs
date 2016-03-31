/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections;
using System.Reflection;
using System.Management.Automation;
using System.Management.Automation.Internal;

using Microsoft.PowerShell.Commands.Internal.Format;

namespace Microsoft.PowerShell.Commands
{

    /// <summary>
    /// implementation for the out-lineoutput command
    /// it provides a wrapper for the OutCommandInner class,
    /// which is the general purpose output command
    /// </summary>
    [Cmdlet ("Out", "LineOutput")]
    public class OutLineOutputCommand : FrontEndCommandBase
    {
        /// <summary>
        /// command line switch for ILineOutput comunication channel
        /// </summary>
        /// <value></value>
        [Parameter(Mandatory = true, Position = 0)]
        public object LineOutput
        {
            get { return lineOutput; }
            set { lineOutput = value; }
        }

        private object lineOutput = null;


        /// <summary>
        /// set inner command
        /// </summary>
        public OutLineOutputCommand ()
        {
            this.implementation = new OutCommandInner ();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void BeginProcessing ()
        {
            if (this.lineOutput == null)
            {
                ProcessNullLineOutput ();
            }

            LineOutput lo = this.lineOutput as LineOutput;
            if (lo == null)
            {
                ProcessWrongTypeLineOutput (this.lineOutput);
            }
            ((OutCommandInner)this.implementation).LineOutput = lo;

            base.BeginProcessing ();
        }

        private void ProcessNullLineOutput ()
        {
            string msg = StringUtil.Format(FormatAndOut_out_xxx.OutLineOutput_NullLineOutputParameter);

            ErrorRecord errorRecord = new ErrorRecord (
                PSTraceSource.NewArgumentNullException("LineOutput"),
                "OutLineOutputNullLineOutputParameter",
                ErrorCategory.InvalidArgument,
                null);

            errorRecord.ErrorDetails = new ErrorDetails (msg);
            this.ThrowTerminatingError (errorRecord);
        }

        private void ProcessWrongTypeLineOutput (object obj)
        {
            string msg = StringUtil.Format(FormatAndOut_out_xxx.OutLineOutput_InvalidLineOutputParameterType,
                obj.GetType().FullName,
                typeof(LineOutput).FullName);

            ErrorRecord errorRecord = new ErrorRecord (
                new InvalidCastException (),
                "OutLineOutputInvalidLineOutputParameterType",
                ErrorCategory.InvalidArgument,
                null);

            errorRecord.ErrorDetails = new ErrorDetails (msg);
            this.ThrowTerminatingError (errorRecord);
        }
    }
}




