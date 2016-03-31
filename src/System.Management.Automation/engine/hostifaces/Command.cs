/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dbg = System.Management.Automation.Diagnostics;
using System.Management.Automation.Internal;
using Microsoft.Management.Infrastructure;

namespace System.Management.Automation.Runspaces
{

    /// <summary>
    /// Defines a Command object which can be added to <see cref="Pipeline"/> object
    /// for invocation.
    /// </summary>
    public sealed class Command 
    {
        #region constructors
        
        /// <summary>
        /// Initializes a new instance of Command class using specified command parameter.
        /// </summary>
        /// <param name="command">Name of the command or script contents </param>
        /// <exception cref="ArgumentNullException">command is null</exception>
        public Command (string command)
            :this(command,false,null)
        {
        }

        /// <summary>
        /// Initializes a new instance of Command class using specified command parameter.
        /// </summary>
        /// <param name="command">The command name or script contents</param>
        /// <param name="isScript">True if this command represents a script, otherwise; false.</param>
        /// <exception cref="ArgumentNullException">command is null</exception>
        public Command (string command, bool isScript)
            :this(command,isScript,null)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="command">The command name or script contents</param>
        /// <param name="isScript">True if this command represents a script, otherwise; false.</param>
        /// <param name="useLocalScope">if true local scope is used to run the script command</param>
        /// <exception cref="ArgumentNullException">command is null</exception>
        public Command(string command, bool isScript, bool useLocalScope)
        {
            IsEndOfStatement = false;
            if (command == null)
            {
                throw PSTraceSource.NewArgumentNullException ("command");
            }

            _command = command;
            _isScript = isScript;
            _useLocalScope = useLocalScope;
        }

        internal Command(string command, bool isScript, bool? useLocalScope)
        {
            IsEndOfStatement = false;
            if (command == null)
            {
                throw PSTraceSource.NewArgumentNullException("command");
            }

            _command = command;
            _isScript = isScript;
            _useLocalScope = useLocalScope;
        }

        internal Command(string command, bool isScript, bool? useLocalScope, bool mergeUnclaimedPreviousErrorResults)
            : this(command, isScript, useLocalScope)
        {
            if (mergeUnclaimedPreviousErrorResults)
            {
                _mergeUnclaimedPreviousCommandResults = PipelineResultTypes.Error | PipelineResultTypes.Output;
            }
        }

        internal Command(CommandInfo commandInfo)
            :this(commandInfo, false)
        {
        }

        internal Command(CommandInfo commandInfo, bool isScript)
        {
            IsEndOfStatement = false;
            _commandInfo = commandInfo;
            _command = _commandInfo.Name;
            _isScript = isScript;
        }

        /// <summary>
        /// Copy constructor for clone operations
        /// </summary>
        /// <param name="command">The source <see cref="Command"/> instance.</param>
        internal Command (Command command)
        {
            _isScript = command._isScript;
            _useLocalScope = command._useLocalScope;
            _command = command._command;
            _mergeInstructions = command._mergeInstructions;
            _mergeMyResult = command._mergeMyResult;
            _mergeToResult = command._mergeToResult;
            _mergeUnclaimedPreviousCommandResults = command._mergeUnclaimedPreviousCommandResults;
            IsEndOfStatement = command.IsEndOfStatement;

            foreach (CommandParameter param in command.Parameters)
            {
                Parameters.Add (new CommandParameter (param.Name, param.Value));
            }
        }

        #endregion constructors

        #region Properties

        /// <summary>
        /// Gets the set of parameters for this command.
        /// </summary>
        /// <remarks>
        /// This property is used to add positional or named parameters to the command.
        /// </remarks>
        public CommandParameterCollection Parameters
        {
            get { return _parameters; }
        }

        /// <summary>
        /// Access the command string.
        /// </summary>
        /// <value>The command name, if <see cref="Command.IsScript"/> is false; otherwise; the script contents</value>
        public string CommandText
        {
            get { return _command; }
        }

        /// <summary>
        /// Access the commandInfo.
        /// </summary>
        /// <value>The command info object</value>
        internal CommandInfo CommandInfo
        {
            get { return _commandInfo; }
        }

        /// <summary>
        /// Access the value indicating if this <see cref="Command"/> represents a script.
        /// </summary>
        public bool IsScript
        {
            get { return _isScript; }
        }

        /// <summary>
        /// Access the value indicating if LocalScope is to be used for running
        /// this script command.
        /// </summary>
        /// <value>True if this command is a script and localScope is 
        /// used for executing the script</value>
        /// <remarks>This value is always false for non-script commands</remarks>
        public bool UseLocalScope
        {
            get { return _useLocalScope ?? false; }
        }

        /// <summary>
        /// Gets or sets the command origin for this command. A command origin
        /// of 'Runspace' (the default) applies Runspace restrictions to this command.
        /// A command origin of 'Internal' does not apply runspace restrictions.
        /// </summary>
        public CommandOrigin CommandOrigin
        {
            get { return _commandOrigin; }
            set { _commandOrigin = value; }
        }
        private CommandOrigin _commandOrigin = CommandOrigin.Runspace;

        /// <summary>
        /// Access the actual value indicating if LocalScope is to be used for running
        /// this script command.  Needed for serialization in remoting.
        /// </summary>
        internal bool? UseLocalScopeNullable
        {
            get { return _useLocalScope; }
        }

        /// <summary>
        /// Checks if the current command marks the end of a statement (see PowerShell.AddStatement())
        /// </summary>
        public bool IsEndOfStatement { get; internal set; }

        #endregion Properties

        #region Methods
        
        /// <summary>
        /// Creates a new <see cref="Command"/> that is a copy of the current instance.
        /// </summary>
        /// <returns>A new <see cref="Command"/> that is a copy of this instance.</returns>
        internal Command Clone ()
        {
            return new Command (this);
        }

        /// <summary>
        /// for diagnostic purposes
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _command;
        }

        #endregion Methods

        #region Merge
        
        private PipelineResultTypes _mergeUnclaimedPreviousCommandResults =
            PipelineResultTypes.None;
        /// <summary>
        /// Sets this command as the mergepoint for previous unclaimed 
        /// commands' results
        /// </summary>
        /// <value></value>
        /// <remarks>
        /// Currently only supported operation is to merge 
        /// Output and Error.
        /// </remarks>
        /// <exception cref="NotSupportedException">
        /// Currently only supported operation is to merge Output and Error.
        /// Attempt to set the property to something other than
        /// PipelineResultTypes.Error | PipelineResultTypes.Output results
        /// in this exception.
        /// </exception>
        public PipelineResultTypes MergeUnclaimedPreviousCommandResults
        {
            get
            {
                return _mergeUnclaimedPreviousCommandResults;
            }
            set
            {
                if (value == PipelineResultTypes.None)
                {
                    _mergeUnclaimedPreviousCommandResults = value;
                    return;
                }

                if (value != (PipelineResultTypes.Error | PipelineResultTypes.Output))
                {
                    throw PSTraceSource.NewNotSupportedException();
                }
                
                _mergeUnclaimedPreviousCommandResults = value;
            }
        }

        //
        // These properties are kept for backwards compatibility for V2
        // over the wire, which allows merging only for Error stream.
        //
        private PipelineResultTypes _mergeMyResult = PipelineResultTypes.None;
        private PipelineResultTypes _mergeToResult = PipelineResultTypes.None;

        internal PipelineResultTypes MergeMyResult
        {
            get { return _mergeMyResult; }
        }

        internal PipelineResultTypes MergeToResult
        {
            get { return _mergeToResult; }
        }

        //
        // For V3 we allow merging from all streams except Output.
        //
        internal enum MergeType
        {
            Error = 0,
            Warning = 1,
            Verbose = 2,
            Debug = 3,
            Information = 4
        }
        internal const int MaxMergeType = (int)(MergeType.Information + 1);
        private PipelineResultTypes[] _mergeInstructions = new PipelineResultTypes[MaxMergeType];

        /// <summary>
        /// Internal accessor for _mergeInstructions. It is used by serialization
        /// code
        /// </summary>
        internal PipelineResultTypes[] MergeInstructions
        {
            get { return _mergeInstructions; }
            set { _mergeInstructions = value; }
        }

        /// <summary>
        /// Merges this commands resutls
        /// </summary>
        /// 
        /// <param name="myResult">
        /// Pipeline stream to be redirected.
        /// </param>
        /// 
        /// <param name="toResult">
        /// Pipeline stream in to which myResult is merged
        /// </param>
        /// 
        /// <exception cref="ArgumentException">
        /// myResult parameter is not PipelineResultTypes.Error or
        /// toResult parameter is not PipelineResultTypes.Output
        /// </exception>
        /// <remarks>
        /// Currently only operation supported is to merge error of command to output of
        /// command.
        /// </remarks>
        public void MergeMyResults(PipelineResultTypes myResult, PipelineResultTypes toResult)
        {
            if (myResult == PipelineResultTypes.None && toResult == PipelineResultTypes.None)
            {
                // For V2 backwards compatibility.
                _mergeMyResult = myResult;
                _mergeToResult = toResult;

                for (int i = 0; i < MaxMergeType; ++i)
                {
                    _mergeInstructions[i] = PipelineResultTypes.None;
                }
                return;
            }

            // Validate parameters.
            if (myResult == PipelineResultTypes.None || myResult == PipelineResultTypes.Output)
            {
                throw PSTraceSource.NewArgumentException("myResult", RunspaceStrings.InvalidMyResultError);
            }
            if (myResult == PipelineResultTypes.Error && toResult != PipelineResultTypes.Output)
            {
                throw PSTraceSource.NewArgumentException("toResult", RunspaceStrings.InvalidValueToResultError);
            }
            if (toResult != PipelineResultTypes.Output && toResult != PipelineResultTypes.Null)
            {
                throw PSTraceSource.NewArgumentException("toResult", RunspaceStrings.InvalidValueToResult);
            }

            // For V2 backwards compatibility.
            if (myResult == PipelineResultTypes.Error)
            {
                _mergeMyResult = myResult;
                _mergeToResult = toResult;
            }

            // Set internal merge instructions.
            if (myResult == PipelineResultTypes.Error || myResult == PipelineResultTypes.All)
            {
                _mergeInstructions[(int)MergeType.Error] = toResult;
            }
            if (myResult == PipelineResultTypes.Warning || myResult == PipelineResultTypes.All)
            {
                _mergeInstructions[(int)MergeType.Warning] = toResult;
            }
            if (myResult == PipelineResultTypes.Verbose || myResult == PipelineResultTypes.All)
            {
                _mergeInstructions[(int)MergeType.Verbose] = toResult;
            }
            if (myResult == PipelineResultTypes.Debug || myResult == PipelineResultTypes.All)
            {
                _mergeInstructions[(int)MergeType.Debug] = toResult;
            }
            if (myResult == PipelineResultTypes.Information || myResult == PipelineResultTypes.All)
            {
                _mergeInstructions[(int)MergeType.Information] = toResult;
            }

        }

        /// <summary>
        /// Set the merge settings on commandProcessor
        /// </summary>
        /// <param name="commandProcessor"></param>
        private
        void
        SetMergeSettingsOnCommandProcessor(CommandProcessorBase commandProcessor)
        {
            Dbg.Assert(commandProcessor != null, "caller should valiadate the parameter");

            MshCommandRuntime mcr = commandProcessor.Command.commandRuntime as MshCommandRuntime;

            if (_mergeUnclaimedPreviousCommandResults != PipelineResultTypes.None)
            {
                //Currently only merging previous unclaimed error and output is supported. 
                if (mcr != null)
                {
                    mcr.MergeUnclaimedPreviousErrorResults = true;
                }
            }

            // Error merge.
            if (_mergeInstructions[(int)MergeType.Error] == PipelineResultTypes.Output)
            {
                //Currently only merging error with output is supported.
                mcr.ErrorMergeTo = MshCommandRuntime.MergeDataStream.Output;
            }

            // Warning merge.
            PipelineResultTypes toType = _mergeInstructions[(int)MergeType.Warning];
            if (toType != PipelineResultTypes.None)
            {
                mcr.WarningOutputPipe = GetRedirectionPipe(toType, mcr);
            }

            // Verbose merge.
            toType = _mergeInstructions[(int)MergeType.Verbose];
            if (toType != PipelineResultTypes.None)
            {
                mcr.VerboseOutputPipe = GetRedirectionPipe(toType, mcr);
            }

            // Debug merge.
            toType = _mergeInstructions[(int)MergeType.Debug];
            if (toType != PipelineResultTypes.None)
            {
                mcr.DebugOutputPipe = GetRedirectionPipe(toType, mcr);
            }

            // Information merge.
            toType = _mergeInstructions[(int)MergeType.Information];
            if (toType != PipelineResultTypes.None)
            {
                mcr.InformationOutputPipe = GetRedirectionPipe(toType, mcr);
            }
        }

        private Pipe GetRedirectionPipe(
            PipelineResultTypes toType,
            MshCommandRuntime mcr)
        {
            if (toType == PipelineResultTypes.Output)
            {
                return mcr.OutputPipe;
            }

            Pipe pipe = new Pipe();
            pipe.NullPipe = true;
            return pipe;
        }

        #endregion Merge

        /// <summary>
        /// Create a CommandProcessorBase for this Command
        /// </summary>
        /// <param name="executionContext"></param>
        /// <param name="commandFactory"></param>
        /// <param name="addToHistory"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        internal
        CommandProcessorBase
        CreateCommandProcessor
        (
            ExecutionContext executionContext,
            CommandFactory commandFactory,
            bool addToHistory,
            CommandOrigin origin
        )
        {
            Dbg.Assert(executionContext != null, "Caller should verify the parameters");
            Dbg.Assert(commandFactory != null, "Caller should verify the parameters");


            CommandProcessorBase commandProcessorBase;

            if (IsScript)
            {
                if ((executionContext.LanguageMode == PSLanguageMode.NoLanguage) &&
                    (origin == Automation.CommandOrigin.Runspace))
                {
                    throw InterpreterError.NewInterpreterException(CommandText, typeof(ParseException),
                        null, "ScriptsNotAllowed", ParserStrings.ScriptsNotAllowed);
                }

                ScriptBlock scriptBlock = executionContext.Engine.ParseScriptBlock(CommandText, addToHistory);
                if(origin == Automation.CommandOrigin.Internal)
                {
                    scriptBlock.LanguageMode = PSLanguageMode.FullLanguage;
                }

                // If running in restricted language mode, verify that the parse tree represents on legitimate
                // constructions...
                switch (scriptBlock.LanguageMode)
                {
                case PSLanguageMode.RestrictedLanguage:
                    scriptBlock.CheckRestrictedLanguage(null, null, false);
                    break;
                case PSLanguageMode.FullLanguage:
                    // Interactive script commands are permitted in this mode.
                    break;
                case PSLanguageMode.ConstrainedLanguage:
                    // Constrained Language is checked at runtime.
                    break;
                default:
                    // This should never happen...
                    Diagnostics.Assert(false, "Invalid langage mode was set when building a ScriptCommandProcessor");
                    throw new InvalidOperationException("Invalid langage mode was set when building a ScriptCommandProcessor");
                }

                if (scriptBlock.UsesCmdletBinding)
                {
                    FunctionInfo functionInfo = new FunctionInfo("", scriptBlock, executionContext);
                    commandProcessorBase = new CommandProcessor(functionInfo, executionContext,
                                                                _useLocalScope ?? false, fromScriptFile: false, sessionState: executionContext.EngineSessionState);
                }
                else
                {
                    commandProcessorBase = new DlrScriptCommandProcessor(scriptBlock,
                                                                         executionContext, _useLocalScope ?? false,
                                                                         origin,
                                                                         executionContext.EngineSessionState);
                }
            }
            else
            {
                // RestrictedLanguage / NoLanguage do not support dot-sourcing when CommandOrigin is Runspace
                if (( _useLocalScope.HasValue ) && ( !_useLocalScope.Value ))
                {
                    switch (executionContext.LanguageMode)
                    {
                        case PSLanguageMode.RestrictedLanguage:
                        case PSLanguageMode.NoLanguage:
                            string message = StringUtil.Format(RunspaceStrings.UseLocalScopeNotAllowed,
                                "UseLocalScope",
                                PSLanguageMode.RestrictedLanguage.ToString(),
                                PSLanguageMode.NoLanguage.ToString());
                            throw new RuntimeException(message);
                        case PSLanguageMode.FullLanguage:
                            // Interactive script commands are permitted in this mode...
                            break;
                    }
                }

                commandProcessorBase =
                    commandFactory.CreateCommand(CommandText, origin, _useLocalScope);
            }

            CommandParameterCollection parameters = Parameters;

            if (parameters != null)
            {
                bool isNativeCommand = commandProcessorBase is NativeCommandProcessor;
                foreach (CommandParameter publicParameter in parameters)
                {
                    CommandParameterInternal internalParameter = CommandParameter.ToCommandParameterInternal(publicParameter, isNativeCommand);
                    commandProcessorBase.AddParameter(internalParameter);
                }
            }

            string helpTarget;
            HelpCategory helpCategory;
            if (commandProcessorBase.IsHelpRequested(out helpTarget, out helpCategory))
            {
                commandProcessorBase = CommandProcessorBase.CreateGetHelpCommandProcessor(
                    executionContext, 
                    helpTarget, 
                    helpCategory);
            }

            //Set the merge settings
            SetMergeSettingsOnCommandProcessor(commandProcessorBase);

            return commandProcessorBase;
        }

        #region Private fields

        /// <summary>
        /// The collection of paramters that have been added.
        /// </summary>
        private readonly CommandParameterCollection _parameters = new CommandParameterCollection ();

        /// <summary>
        /// The command string passed in at ctor time.
        /// </summary>
        private readonly string          _command = string.Empty;

        /// <summary>
        /// The command info passed in at ctor time.
        /// </summary>
        private readonly CommandInfo _commandInfo;

        /// <summary>
        /// Does this instance represent a script?
        /// </summary>
        private readonly bool            _isScript;
        
        /// <summary>
        /// This is used for script commands (i.e. _isScript is true). If 
        /// _useLocalScope is true, script is run in LocalScope.  If
        /// null, it was unspecified and a suitable default is used (true
        /// for non-script, false for script).  Note that the public
        /// property is bool, not bool? (from V1), so it should probably
        /// be deprecated, at least for internal use.
        /// </summary>
        private bool?           _useLocalScope;

        #endregion Private fields

        #region Serialization / deserialization for remoting


        /// <summary>
        /// Creates a Command object from a PSObject property bag. 
        /// PSObject has to be in the format returned by ToPSObjectForRemoting method.
        /// </summary>
        /// <param name="commandAsPSObject">PSObject to rehydrate</param>
        /// <returns>
        /// Command rehydrated from a PSObject property bag
        /// </returns>       
        /// <exception cref="ArgumentNullException">
        /// Thrown if the PSObject is null.
        /// </exception>
        /// <exception cref="System.Management.Automation.Remoting.PSRemotingDataStructureException">
        /// Thrown when the PSObject is not in the expected format
        /// </exception>
        static internal Command FromPSObjectForRemoting(PSObject commandAsPSObject)
        {
            if (commandAsPSObject == null)
            {
                throw PSTraceSource.NewArgumentNullException("commandAsPSObject");
            }

            string commandText = RemotingDecoder.GetPropertyValue<string>(commandAsPSObject, RemoteDataNameStrings.CommandText);
            bool isScript = RemotingDecoder.GetPropertyValue<bool>(commandAsPSObject, RemoteDataNameStrings.IsScript);
            bool? useLocalScopeNullable = RemotingDecoder.GetPropertyValue<bool?>(commandAsPSObject, RemoteDataNameStrings.UseLocalScopeNullable);
            Command command = new Command(commandText, isScript, useLocalScopeNullable);

            // For V2 backwards compatibility.
            PipelineResultTypes mergeMyResult = RemotingDecoder.GetPropertyValue<PipelineResultTypes>(commandAsPSObject, RemoteDataNameStrings.MergeMyResult);
            PipelineResultTypes mergeToResult = RemotingDecoder.GetPropertyValue<PipelineResultTypes>(commandAsPSObject, RemoteDataNameStrings.MergeToResult);
            command.MergeMyResults(mergeMyResult, mergeToResult);

            command.MergeUnclaimedPreviousCommandResults = RemotingDecoder.GetPropertyValue<PipelineResultTypes>(commandAsPSObject, RemoteDataNameStrings.MergeUnclaimedPreviousCommandResults);

            // V3 merge instructions will not be returned by V2 server and this is expected.
            if (commandAsPSObject.Properties[RemoteDataNameStrings.MergeError] != null)
            {
                command.MergeInstructions[(int)MergeType.Error] = RemotingDecoder.GetPropertyValue<PipelineResultTypes>(commandAsPSObject, RemoteDataNameStrings.MergeError);
            }
            if (commandAsPSObject.Properties[RemoteDataNameStrings.MergeWarning] != null)
            {
                command.MergeInstructions[(int)MergeType.Warning] = RemotingDecoder.GetPropertyValue<PipelineResultTypes>(commandAsPSObject, RemoteDataNameStrings.MergeWarning);
            }
            if (commandAsPSObject.Properties[RemoteDataNameStrings.MergeVerbose] != null)
            {
                command.MergeInstructions[(int)MergeType.Verbose] = RemotingDecoder.GetPropertyValue<PipelineResultTypes>(commandAsPSObject, RemoteDataNameStrings.MergeVerbose);
            }
            if (commandAsPSObject.Properties[RemoteDataNameStrings.MergeDebug] != null)
            {
                command.MergeInstructions[(int)MergeType.Debug] = RemotingDecoder.GetPropertyValue<PipelineResultTypes>(commandAsPSObject, RemoteDataNameStrings.MergeDebug);
            }
            if (commandAsPSObject.Properties[RemoteDataNameStrings.MergeInformation] != null)
            {
                command.MergeInstructions[(int)MergeType.Information] = RemotingDecoder.GetPropertyValue<PipelineResultTypes>(commandAsPSObject, RemoteDataNameStrings.MergeInformation);
            }

            foreach (PSObject parameterAsPSObject in RemotingDecoder.EnumerateListProperty<PSObject>(commandAsPSObject, RemoteDataNameStrings.Parameters))
            {
                command.Parameters.Add(CommandParameter.FromPSObjectForRemoting(parameterAsPSObject));
            }

            return command;
        }

        /// <summary>
        /// Returns this object as a PSObject property bag
        /// that can be used in a remoting protocol data object.
        /// </summary>
        /// <param name="psRPVersion">PowerShell remoting protocol version</param>
        /// <returns>This object as a PSObject property bag</returns>
        internal PSObject ToPSObjectForRemoting(Version psRPVersion)
        {
            PSObject commandAsPSObject = RemotingEncoder.CreateEmptyPSObject();

            commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.CommandText, this.CommandText));
            commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.IsScript, this.IsScript));
            commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.UseLocalScopeNullable, this.UseLocalScopeNullable));

            // For V2 backwards compatibility.
            commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeMyResult, this.MergeMyResult));
            commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeToResult, this.MergeToResult));

            commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeUnclaimedPreviousCommandResults, this.MergeUnclaimedPreviousCommandResults));


            if (psRPVersion != null &&
                psRPVersion >= RemotingConstants.ProtocolVersionWin10RTM)
            {
                // V5 merge instructions
                commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeError, _mergeInstructions[(int)MergeType.Error]));
                commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeWarning, _mergeInstructions[(int)MergeType.Warning]));
                commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeVerbose, _mergeInstructions[(int)MergeType.Verbose]));
                commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeDebug, _mergeInstructions[(int)MergeType.Debug]));
                commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeInformation, _mergeInstructions[(int)MergeType.Information]));
            }
            else if (psRPVersion != null &&
                psRPVersion >= RemotingConstants.ProtocolVersionWin8RTM)
            {
                // V3 merge instructions.
                commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeError, _mergeInstructions[(int)MergeType.Error]));
                commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeWarning, _mergeInstructions[(int)MergeType.Warning]));
                commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeVerbose, _mergeInstructions[(int)MergeType.Verbose]));
                commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.MergeDebug, _mergeInstructions[(int)MergeType.Debug]));

                // If they've explicitly redirected the Information stream, generate an error. Don't
                // generate an error if they've done "*", as that makes any new stream a breaking change.
                if ((_mergeInstructions[(int)MergeType.Information] == PipelineResultTypes.Output) &&
                    (_mergeInstructions.Length != MaxMergeType))
                {
                    throw new RuntimeException(
                        StringUtil.Format(RunspaceStrings.InformationRedirectionNotSupported));
                }
            }
            else
            {
                // If they've explicitly redirected an unsupported stream, generate an error. Don't
                // generate an error if they've done "*", as that makes any new stream a breaking change.
                if (_mergeInstructions.Length != MaxMergeType)
                {
                    if (_mergeInstructions[(int)MergeType.Warning] == PipelineResultTypes.Output)
                    {
                        throw new RuntimeException(
                            StringUtil.Format(RunspaceStrings.WarningRedirectionNotSupported));
                    }

                    if (_mergeInstructions[(int)MergeType.Verbose] == PipelineResultTypes.Output)
                    {
                        throw new RuntimeException(
                            StringUtil.Format(RunspaceStrings.VerboseRedirectionNotSupported));
                    }

                    if (_mergeInstructions[(int)MergeType.Debug] == PipelineResultTypes.Output)
                    {
                        throw new RuntimeException(
                            StringUtil.Format(RunspaceStrings.DebugRedirectionNotSupported));
                    }

                    if (_mergeInstructions[(int)MergeType.Information] == PipelineResultTypes.Output)
                    {
                        throw new RuntimeException(
                            StringUtil.Format(RunspaceStrings.InformationRedirectionNotSupported));
                    }
                }
            }

            List<PSObject> parametersAsListOfPSObjects = new List<PSObject>(this.Parameters.Count);
            foreach (CommandParameter parameter in this.Parameters)
            {
                parametersAsListOfPSObjects.Add(parameter.ToPSObjectForRemoting());
            }
            commandAsPSObject.Properties.Add(new PSNoteProperty(RemoteDataNameStrings.Parameters, parametersAsListOfPSObjects));

            return commandAsPSObject;
        }


        #endregion

        #region Win Blue Extensions

#if !CORECLR // PSMI Not Supported On CSS
        internal CimInstance ToCimInstance()
        {
            CimInstance c = InternalMISerializer.CreateCimInstance("PS_Command");
            CimProperty commandTextProperty = InternalMISerializer.CreateCimProperty("CommandText", 
                                                                                     this.CommandText, 
                                                                                     Microsoft.Management.Infrastructure.CimType.String);
            c.CimInstanceProperties.Add(commandTextProperty);
            CimProperty isScriptProperty = InternalMISerializer.CreateCimProperty("IsScript", 
                                                                                  this.IsScript, 
                                                                                  Microsoft.Management.Infrastructure.CimType.Boolean);
            c.CimInstanceProperties.Add(isScriptProperty);

            if (this.Parameters != null && this.Parameters.Count > 0)
            {
                List<CimInstance> parameterInstances = new List<CimInstance>();
                foreach (var p in this.Parameters)
                {
                    parameterInstances.Add(p.ToCimInstance());
                }

                if (parameterInstances.Count > 0)
                {
                    CimProperty parametersProperty = InternalMISerializer.CreateCimProperty("Parameters", 
                                                                                            parameterInstances.ToArray(),
                                                                                            Microsoft.Management.Infrastructure.CimType.ReferenceArray);
                    c.CimInstanceProperties.Add(parametersProperty);
                }
            }

            return c;
        }
#endif

        #endregion Win Blue Extensions
    }

    /// <summary>
    /// Enum defining the types of streams coming out of a pipeline
    /// </summary>
    [Flags]
    public enum PipelineResultTypes
    {
        /// <summary>
        /// Default streaming behavior
        /// </summary>
        None,

        /// <summary>
        /// Success output 
        /// </summary>
        Output,

        /// <summary>
        /// Error output 
        /// </summary>
        Error,

        /// <summary>
        /// Warning information stream
        /// </summary>
        Warning,

        /// <summary>
        /// Verbose information stream
        /// </summary>
        Verbose,

        /// <summary>
        /// Debug information stream
        /// </summary>
        Debug,

        /// <summary>
        /// Information information stream
        /// </summary>
        Information,

        /// <summary>
        /// All streams
        /// </summary>
        All,

        /// <summary>
        /// Redirect to nothing.
        /// </summary>
        Null
    }

    /// <summary>
    /// Defines a collection of Commands. This collection is used by <see cref="Pipeline"/> to define
    /// elements of pipeline.
    /// </summary>
    public sealed class CommandCollection : Collection<Command>
    {
        /// <summary>
        /// Make the default constructor internal
        /// </summary>
        internal CommandCollection ()
        {
        }
        
        /// <summary>
        /// Adds a new command for given string
        /// </summary>
        /// <exception cref="System.ArgumentNullException">
        /// command is null.
        /// </exception>
        public void Add (string command)
        {
            if (String.Equals(command, "out-default", StringComparison.OrdinalIgnoreCase))
            {
                this.Add(command, true);
            }
            else
            {
                this.Add(new Command(command));
            }
        }

        internal void Add(string command, bool mergeUnclaimedPreviousCommandError)
        {
            this.Add(new Command(command, false, false, mergeUnclaimedPreviousCommandError));
        }

        /// <summary>
        /// Adds a new script command
        /// </summary>
        /// <param name="scriptContents">script contents</param>
        /// <exception cref="System.ArgumentNullException">
        /// scriptContents is null.
        /// </exception>
        public void AddScript (string scriptContents)
        {
            this.Add (new Command (scriptContents, true));
        }

        /// <summary>
        /// Adds a new scrip command for given script
        /// </summary>
        /// <param name="scriptContents">script contents</param>
        /// <param name="useLocalScope">if true local scope is used to run the script command</param>
        /// <exception cref="System.ArgumentNullException">
        /// scriptContents is null.
        /// </exception>
        public void AddScript (string scriptContents, bool useLocalScope)
        {
            this.Add (new Command (scriptContents, true, useLocalScope));
        }

        /// <summary>
        /// Gets the string represenation of the command collection to be used for history.
        /// </summary>
        /// <returns>
        /// string representing the command(s)
        /// </returns>
        internal string GetCommandStringForHistory()
        {
            Diagnostics.Assert(this.Count != 0, "this is called when there is at least one element in the collection");
            Command firstCommand = this[0];
            return firstCommand.CommandText;
        }
    }
}

