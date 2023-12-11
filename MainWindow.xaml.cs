#define SavePdfViaTeddsApplication

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Threading.Tasks;

using Microsoft.Win32;

using Tekla.Structural.InteropAssemblies.Tedds;
using Tekla.Structural.InteropAssemblies.TeddsCalc;

using Application = Tekla.Structural.InteropAssemblies.Tedds.Application;

namespace TeddsAPITester
{
    /// <summary>
    /// Structure of the input and output data required for calculating
    /// </summary>
    public class CalculationData
    {
        /// <summary>Instance variable <c>ShowUserInterface</c> determines whether the user interface of the calculation is show or hidden.</summary>
        public bool ShowUserInterface { get; set; }
        /// <summary>Instance variable <c>CreateOutputRtf</c> determines whether output is required.</summary>
        public bool CreateOutputRtf { get; set; }
        /// <summary>Instance variable <c>CalculatingProgressEvents</c> determines whether to listen for calculation progress events.</summary>
        public bool CalculatingProgressEvents { get; set; }
        /// <summary>Instance variable <c>UndefinedVariableEvents</c> determines whether to listen for undefined variable events.</summary>
        public bool UndefinedVariableEvents { get; set; }
        /// <summary>Instance variable <c>ErrorEvents</c> determines whether to listen to error events.</summary>
        public bool ErrorEvents { get; set; }
        /// <summary>Instance variable <c>InputVariablesXml</c> which represents the input variables for the calculation in the Tedds variables XML file format.</summary>
        public string InputVariablesXml { get; set; }
        /// <summary>Instance variable <c>CalcFileName</c> full path of the Calc Library file which contains the Calc Item to calculate.</summary>
        public string CalcFileName { get; set; }
        /// <summary>Instance variable <c>CalcItemName</c> short name of the Calc Item to calculate.</summary>
        public string CalcItemName { get; set; }
        /// <summary>Instance variable <c>OutputVariablesXml</c> which is the output variables of a calculation in the Tedds variables XML file format</summary>
        public string OutputVariablesXml { get; set; }
        /// <summary>Instance variable <c>OutputRtf</c></summary>
        public string OutputRtf { get; set; }
        /// <summary>Instance variable <c>OutputPdf</c> which is the output of a calculation in PDF format.</summary>
        public string OutputPdf { get; set; }
    }

    /// <summary>
    /// The Tedds API Tester is designed for testing or learning how to use the Tedds API, it can be used to calculate an existing 
    /// Tedds calculation which is stored in a Tedds Calc Library. The output variables produced by running a 
    /// calculation can be saved and then used as the input for a subsequent run of the calculation. Typically you would 
    /// run the calculation once with the user interface enabled allowing you to setup the default input for a 
    /// design. Subsequent calculations can then be started using this default input with further changes 
    /// applied to the input in your own code in order to automate a design work flow. 
    /// 
    /// Please note: This example is not production code in particular input validation and exception handling is generally 
    /// omitted or very basic
    /// 
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Main window constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        #region "Calculate Methods"

        /// <summary>
        /// Execute a calculation using the specified optional input variables and return only 
        /// the output variables XML. Omitting the RTF output when it is not needed will improve performance.
        /// </summary>
        /// <returns>Returns all the calculated variables in the Tedds variables XML file format.</returns>
        public void CalculateNoOutputRtf(ref CalculationData data)
        {
            //Create calculator instance and initialize with input
            Calculator calculator = new Calculator();
            User32Native.SetForegroundWindow((IntPtr)calculator.WindowHandle);

            try
            {
                ConnectEvents(ref calculator, data);

                DisableUI();

                calculator.Initialize(null, data.InputVariablesXml);

                //Apply additional settings/variables
                calculator.Functions.SetVar("_CalcUI", data.ShowUserInterface ? 1 : 0);

                //Calculate calculation
                calculator.Functions.Eval($"EvalCalcItem( \"{data.CalcFileName}\", \"{data.CalcItemName}\" )");

                //Get output variables
                data.OutputVariablesXml = calculator.GetVariables();
            }
            finally
            {
                EnableUI();
            }
        }
        /// <summary>
        /// Execute a calculation using the specified optional input variables and return both 
        /// the output variables XML and the output document RTF.
        /// </summary>
        /// <param name="data">Calculation data.</param>
        public void CalculateOutputRtf(ref CalculationData data)
        {
            //Initialize output
            data.OutputVariablesXml = data.OutputRtf = data.OutputPdf = null;

            if (!data.ShowUserInterface)
            {
                //Create first calculator instance which is only required for retrieving input variables
                Calculator calculator = new Calculator();
                User32Native.SetForegroundWindow((IntPtr)calculator.WindowHandle);

                //Apply additional settings/variables to existing input
                calculator.Initialize(null, data.InputVariablesXml);
                calculator.Functions.SetVar("_CalcUI", 0);
                data.InputVariablesXml = calculator.GetVariables();
            }

            //Initialize second calculator but this time with input variables
            Calculator calculator2 = new Calculator();
            User32Native.SetForegroundWindow((IntPtr)calculator2.WindowHandle);

            ConnectEvents(ref calculator2, data);

            try
            {
                DisableUI();
                calculator2.InitializeCalc(data.CalcFileName, data.CalcItemName, data.InputVariablesXml);

                if (calculator2.Status == CalcStatus.Ok ||
                    calculator2.Status == CalcStatus.Interrupted)
                {
                    //Retrieve output
                    data.OutputRtf = calculator2.GetOutput(OutputFormat.Rtf);
                    data.OutputVariablesXml = calculator2.GetVariables();
                    data.OutputPdf = calculator2.GetOutput(OutputFormat.Pdf);
                }
            }
            finally
            {
                EnableUI();
            }
        }
        /// <summary>
        /// Run the calculation process
        /// </summary>
        /// <param name="data">Calculation data.
        private void Calculate(ref CalculationData data)
        {
            if (data.CreateOutputRtf)
                CalculateOutputRtf(ref data);
            else
                CalculateNoOutputRtf(ref data);
        }
        #endregion

        #region "Calculator Events"
        /// <summary>
        /// Calculating progress event handler
        /// 
        /// IMPORTANT NOTE: Responding to progress events can significantly affect calculation performance!
        /// 
        /// </summary>
        /// <param name="progressEvent">Event type</param>
        /// <param name="value">Event value</param>
        /// <param name="text">Event text (if applicable)</param>
        /// <param name="status">Return status</param>
        public void CalculatingProgress(CalcProgressEvent progressEvent, uint value, string text, ref CalcStatus status)
        {
            //Dispatch UI commands to the UI thread
            Dispatcher.Invoke(() =>
            {
                switch (progressEvent)
                {
                    case CalcProgressEvent.ProgressReset:
                        _progressBar.Visibility = Visibility.Visible;
                        _progressBar.Minimum = 0;
                        _progressBar.Maximum = value;
                        _progressBar.Value = 0;
                        break;

                    case CalcProgressEvent.ProgressSetPos:
                        _progressBar.Value = value;
                        break;

                    case CalcProgressEvent.ProgressSetText:
                        StatusText = text;
                        break;

                    case CalcProgressEvent.ProgressAddOutput:
                        StatusText = $"{value}: {text}";
                        break;

                    case CalcProgressEvent.ProgressShow:
                        _progressBar.Visibility = Visibility.Visible;
                        break;

                    case CalcProgressEvent.ProgressFinished:
                    case CalcProgressEvent.ProgressHide:
                        _progressBar.Visibility = Visibility.Hidden;
                        break;
                }
            });

            //Change status to stop the calculation process
            //status = CalcStatus.Aborted;
        }
        /// <summary>
        /// Calculation error event handler
        /// </summary>
        /// <param name="errorType">Type of error</param>
        /// <param name="errorCode">Unique error identifier code</param>
        /// <param name="context">Error context</param>
        /// <param name="message">Error message</param>
        /// <param name="expression">Expression which caused the error, limited to the first 1024 characters</param>
        /// <param name="options">Error options</param>
        /// <param name="status">Return status</param>
        private void CalculatingError(CalcErrorType errorType, uint errorCode, string context, string message, string expression, uint options, ref CalcStatus status)
        {
            MessageBoxButton button = (errorType == CalcErrorType.ErrorExpression) ? MessageBoxButton.OKCancel : MessageBoxButton.OK;
            MessageBoxResult result = MessageBoxResult.OK;

            //Dispatch UI commands to the UI thread
            Dispatcher.Invoke(() =>
            {
                result = MessageBox.Show(
                    this,
                    $"An error has occurred whilst calculating\n\ncontext: {context}\nExpression: {expression}\n\n{message}\n",
                    "Calculation Error",
                    button,
                    MessageBoxImage.Error);
            });

            if (errorType == CalcErrorType.ErrorExpression && result != MessageBoxResult.OK)
                status = CalcStatus.Aborted; //status = CalcStatus.Interrupted; //Interrupt to show the error in the output and keep the current variables
        }
        /// <summary>
        /// Undefined variable error event handler
        /// </summary>
        /// <param name="variableName">Name of undefined variable</param>
        /// <param name="value">Expression to return as the variables new value</param>
        private void UndefinedVariable(string variableName, out string value)
        {
            string input = null;
            Dispatcher.Invoke(() =>
            {
                SimpleInputDialog inputDialog = new SimpleInputDialog();
                inputDialog.Title = "Undefined variable";
                inputDialog.Description = $"Enter a value for '{variableName}' (include units)";
                inputDialog.Owner = this;
                if (inputDialog.ShowDialog() == true)
                    input = inputDialog.Input;
            });
            value = input;
        }
        #endregion

        #region "Document serialization methods"

        /// <summary>
        /// Using the Tedds application create a Tedds (.ted) document which can be opened in the Tedds application directly or imported into Tedds for Word.
        /// </summary>
        /// <param name="fileName">Output file name</param>
        public void SaveTeddsDocument(string fileName)
        {
            SaveWithTedds(fileName, document => document.SaveAs(fileName));
        }
        /// <summary>
        /// Using the Tedds application create a PDF file.
        /// </summary>
        /// <param name="fileName">Output file name</param>
        public void SaveTeddsPdf(string fileName)
        {
            SaveWithTedds(fileName, document => document.SaveAsPdf(fileName));
        }
        /// <summary>
        /// Using the Tedds application, create a document with the given filename.
        /// Then perform the provided action with the tedds document.
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="saver">Action to perform with the created Tedds document</param>
        private void SaveWithTedds(string fileName, Action<ITeddsDocument> saver)
        {
            IApplication application = null;
            ITeddsDocuments documents = null;
            ITeddsDocument document = null;
            try
            {
                //Launch Tedds application, or get hold of an already running instance
                application = new Application();
#if DEBUG
                //If Tedds was not already running, make it visible for debugging purposes
                application.Visible = true;
#endif

                documents = application.Documents;
                //Create a new Tedds document
                document = documents.Add3(Path.GetFileNameWithoutExtension(fileName), CalcFileName, CalcItemName, OutputVariablesXml, OutputRtf);
                //Perform the given save action on the document
                saver(document);

                if (!application.Visible)
                {
                    document.Close(false);
                    //Once the document has been closed this reference is no longer valid and does not need to be freed later
                    document = null;
                }

                StatusText = "Tedds document created successfully";
            }
            catch (Exception ex)
            {
                SetStatus(ex);
            }
            finally
            {
                //Release objects to close down Tedds application
                if (document != null)
                    Marshal.ReleaseComObject(document);
                if (documents != null)
                    Marshal.ReleaseComObject(documents);
                if (application != null)
                    Marshal.ReleaseComObject(application);
            }
        }

        #endregion

        #region "Calc select methods"

        /// <summary>
        /// Browse for a Calc Library.
        /// </summary>
        /// <param name="parentWindow">Parent window of the control</param>
        /// <param name="libraryName">Stores the name of the Calc Library file</param>
        /// <param name="dialogTitle">Title to show on browse dialog</param>
        /// <param name="saveLibrary">Bool specifying whether to save library</param>
        /// <param name="systemDirectories">Bool specifying whether to look for calcs in system directory</param>
        /// <param name="userDirectories">Bool specifying whether to look for calcs in user directory</param>
        /// <param name="usePlaceholders">Bool specifying whether to use place holders in path</param>
        /// <returns>True if a Calc Library was selected, false if the browse dialog was canceled</returns>
        private static bool SelectCalcLibrary(IntPtr parentWindow, ref string libraryName, string dialogTitle = DefaultLibraryTitle,
            bool saveLibrary = false, bool systemDirectories = true, bool userDirectories = true, bool usePlaceholders = true)
        {
            dynamic dlg = Activator.CreateInstance(Type.GetTypeFromProgID("DataLibraryCtrlLib.DialogDataLibraryOpen"));

            dlg.Title = dialogTitle ?? DefaultLibraryTitle;
            if (!string.IsNullOrEmpty(libraryName))
                dlg.FilePath = libraryName;
            dlg.Save = saveLibrary;
            dlg.LookInSystemDirectory = systemDirectories;
            dlg.LookInUserDirectory = userDirectories;
            dlg.ParentWindow = parentWindow;
            dlg.UsePlaceHolders = usePlaceholders;

            if (dlg.Show() == DialogResultCancel)
                return false;

            libraryName = dlg.FilePath;
            return true;
        }

        /// <summary>
        /// Browse for a Calc Item in a given library.
        /// </summary>
        /// <param name="parentWindow">Parent window of the control</param>
        /// <param name="libraryName">Name of Calc Library to browse items in</param>
        /// <param name="itemName">Stores the name of the Calc Item</param>
        /// <param name="dialogTitle">Title to show on browse dialog</param>
        /// <param name="saveItem">Bool specifying whether to save item</param>
        /// <returns>True if a Calc Item was selected, false if the browse dialog was cancelled</returns>
        private static bool SelectCalcItem(IntPtr parentWindow, string libraryName, ref string itemName, string dialogTitle = DefaultItemTitle, bool saveItem = false)
        {
            dynamic dlg = Activator.CreateInstance(Type.GetTypeFromProgID("DataLibraryCtrlLib.DialogDataItemOpen"));

            dlg.Title = dialogTitle ?? DefaultItemTitle;
            dlg.DataLibrary = libraryName;
            if (!string.IsNullOrEmpty(itemName))
                dlg.DataItem = itemName;
            dlg.ParentWindow = parentWindow;
            dlg.Save = saveItem;

            if (dlg.Show() == DialogResultCancel)
                return false;

            itemName = dlg.DataItem;
            return true;
        }

        /// <summary>
        /// Checks whether the Calc Library with the specified name exists.
        /// </summary>
        /// <param name="name">Name of the Calc Library to check, including the system/user path placeholder</param>
        /// <returns>True if the library exists, false if not</returns>
        private static bool DataLibraryExists(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            dynamic libs = Activator.CreateInstance(Type.GetTypeFromProgID("DataLibraryLib.DataLibraries"));
            if (name.StartsWith(UserPrefix))
                libs.UserPath();
            else
                libs.SystemPath();
            return libs.LibraryExists(name.Replace(SysPrefix, string.Empty).Replace(UserPrefix, string.Empty));
        }

        /// <summary>
        /// Checks whether a Calc Item exists in a Calc Library, both specified by name
        /// *NOTE: you must check the specified Calc Library exists before calling this method*
        /// </summary>
        /// <param name="library">Name of the Calc Library to check for the item in, including the system/user path placeholder</param>
        /// <param name="item">Name of the Calc Item to check</param>
        /// <returns>True if the item exists in the library, false if not</returns>
        private static bool DataItemExistsInLibrary(string library, string item)
        {
            if (string.IsNullOrWhiteSpace(item))
                return false;

            dynamic libs = Activator.CreateInstance(Type.GetTypeFromProgID("DataLibraryLib.DataLibraries"));
            return libs.OpenLibrary(library).DataItems.ItemExists(item);
        }

        #endregion

        #region "Events"

        /// <summary>
        /// Calculate button event handler
        /// </summary>
        /// <param name="sender">Sender of event</param>
        /// <param name="e">Event arguments</param>
        private void OnCalculateButtonClick(object sender, RoutedEventArgs e)
        {
            CalculationData data = new CalculationData();
            if (!ValidateInput())
                return;

            ClearResults();

            //Start
            StatusText = $"Started calculating {CalcItemName}...";
            
            //If any events are enabled then the calculation process must be started asynchronously so that 
            //this main thread can listen and respond to those events
            if (IsAsyncCalculatingRequired)
            {
                Task.Run(() =>
                {
                    DoCalculate();
                    //Use invoker so that if this is being run on a worker thread we can update the UI on the main thread
                    Dispatcher.Invoke(UpdateResults);
                });
            }
            else
            {
                DoCalculate();
                UpdateResults();
            }

            //Validate input
            bool ValidateInput()
            {
                //Very basic input validation
                if (!string.IsNullOrEmpty(InputVariablesFileName) && !File.Exists(InputVariablesFileName))
                {
                    StatusText = $"Select a valid input file, '{InputVariablesFileName}' does not exist.";
                    return false;
                }
                if (string.IsNullOrEmpty(CalcFileName) || string.IsNullOrEmpty(CalcItemName))
                {
                    StatusText = "Enter the library file name and the item name of the calculation you want to calculate";
                    return false;
                }

                //Get input
                data.InputVariablesXml = InputVariablesXml;
                data.CalcFileName = CalcFileName;
                data.CalcItemName = CalcItemNameEncoded;
                data.ShowUserInterface = IsShowUserInterfaceEnabled;
                data.CreateOutputRtf = IsCreateOutputRtfEnabled;
                data.CalculatingProgressEvents = CalculatingProgressEvents;
                data.UndefinedVariableEvents = UndefinedVariableEvents;
                data.ErrorEvents = ErrorEvents;

                return true;
            }            
            //Execute the calculation
            void DoCalculate()
            {
                try
                {
                    Calculate(ref data);
                    StatusText = "...finished Calculating";
                }
                catch (Exception ex)
                {
                    SetStatus(ex);
                }
            }
            //Clear output results
            void ClearResults()
            {
                OutputRtf = OutputVariablesXml = null;
            }
            //Update the User interface after calculating
            void UpdateResults()
            {
                OutputVariablesXml = data.OutputVariablesXml;
                OutputRtf = data.OutputRtf;
                OutputPdf = data.OutputPdf;
            }
        }
        /// <summary>
        /// Calculate button event handler
        /// </summary>
        /// <param name="sender">Sender of event</param>
        /// <param name="e">Event arguments</param>
        private void OnSaveAsTeddsDocumentButtonClick(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            { Filter = SaveTedFilter };

            if (saveDialog.ShowDialog(this) == true)
                SaveTeddsDocument(saveDialog.FileName);
        }
        /// <summary>
        /// Exit button event handler
        /// </summary>
        /// <param name="sender">Sender of event</param>
        /// <param name="e">Event arguments</param>
        private void OnExitButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
        /// <summary>
        /// Select input variables button event handler. Browse for xml file to use as input for calculating. 
        /// </summary>
        /// <param name="sender">Sender of event</param>
        /// <param name="e">Event arguments</param>
        private void OnSelectInputVariablesFileClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                FileName = InputVariablesFileName,
                Filter = OpenXmlFilter
            };

            if (fileDialog.ShowDialog(this) == true)
                InputVariablesFileName = fileDialog.FileName;
        }
        /// <summary>
        /// Select calculation file button event handler. Browse for Calc Library.
        /// </summary>
        /// <param name="sender">Sender of event</param>
        /// <param name="e">Event arguments</param>
        private void OnSelectCalculationFileClick(object sender, RoutedEventArgs e)
        {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            string selectedLibraryName = string.Empty;
            if (SelectCalcLibrary(windowHandle, ref selectedLibraryName))
            {
                CalcFileName = selectedLibraryName;
                if (DataLibraryExists(CalcFileName))
                {
                    //If calc item field doesn't already contain an item that exists in the selected library, open the item browse dialog
                    if (!DataItemExistsInLibrary(CalcFileName, CalcItemName))
                        SelectItemHelper();
                }
            }
        }
        /// <summary>
        /// Select calculation item button event handler. Browse for Calc Item within a library.
        /// </summary>
        /// <param name="sender">Sender of event</param>
        /// <param name="e">Event arguments</param>
        private void OnSelectCalculationItemClick(object sender, RoutedEventArgs e)
        {
            if (DataLibraryExists(CalcFileName))
            {
                SelectItemHelper();
            }
            else
            {
                // If library field doesn't contain a valid Library, prompt user to pick one first (which prompts to pick an item in turn)
                OnSelectCalculationFileClick(sender, e);
                //If user still hasn't picked a valid library after prompt (i.e. cancelled), exit with a message
                if (!DataLibraryExists(CalcFileName))
                    MessageBox.Show("Please select a Calc Library before selecting an Item");
            }
        }
        /// <summary>
        /// Helper method to select Calc Item in "click" event handler and to allow selecting Calc Item without
        /// re-checking that the Calc Library exists for cases where this check has already been done
        /// </summary>
        private void SelectItemHelper()
        {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            string selectedItemName = string.Empty;
            if (SelectCalcItem(windowHandle, CalcFileName, ref selectedItemName))
                CalcItemName = selectedItemName;
        }
        /// <summary>
        /// Save output variables button event handler. 
        /// Browse for location to save calculation output variables to as an XML file which can 
        /// be used as the input for a subsequent run of the calculation.
        /// </summary>
        /// <param name="sender">Sender of event</param>
        /// <param name="e">Event arguments</param>
        private void OnSaveAsOutputVariablesXmlClick(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            { Filter = SaveXmlFilter };

            if (saveDialog.ShowDialog(this) == true)
                File.WriteAllText(saveDialog.FileName, OutputVariablesXml);
        }
        /// <summary>
        /// Save output RTF button event handler. Browse for location to save calculation output 
        /// text to as a Rich Text File (RTF).
        /// </summary>
        /// <param name="sender">Sender of event</param>
        /// <param name="e">Event arguments</param>
        private void OnSaveAsOutputRtfClick(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            { Filter = SaveRtfFilter };

            if (saveDialog.ShowDialog(this) == true)
                File.WriteAllText(saveDialog.FileName, OutputRtf);
        }
        /// <summary>
        /// Save output PDF button event handler. Browse for location to save calculation output 
        /// text to as a PDF File.
        /// </summary>
        /// <param name="sender">Sender of event</param>
        /// <param name="e">Event arguments</param>
        private void OnSaveAsOutputPdfClick(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            { Filter = SavePdfFilter };

            if (saveDialog.ShowDialog(this) == true)
            {
#if SavePdfViaTeddsApplication
                SaveTeddsPdf(saveDialog.FileName);
#else
                //PDF is a binary file format so the string isn't actually a properly encoded string, therefore copy raw data
                byte[] bytes = new byte[OutputPdf.Length * sizeof(char)];
                Buffer.BlockCopy(OutputPdf.ToCharArray(), 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(saveDialog.FileName, bytes);
#endif
            }
        }
        #endregion

        #region "Settings"

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            LoadSettings();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            SaveSettings();
        }

        /// <summary>
        /// Load input settings which simplifies using application for repeated testing
        /// </summary>
        private void LoadSettings()
        {
            InputVariablesFileName = Properties.Settings.Default.InputVariableFileName;
            CalcFileName = Properties.Settings.Default.CalcFileName;
            CalcItemName = Properties.Settings.Default.CalcItemName;
            IsShowUserInterfaceEnabled = Properties.Settings.Default.ShowUserInterface;
            IsCreateOutputRtfEnabled = Properties.Settings.Default.CreateOutputRtf;
        }
        /// <summary>
        /// Save input settings which simplifies using application for repeated testing 
        /// </summary>
        private void SaveSettings()
        {
            Properties.Settings.Default.InputVariableFileName = InputVariablesFileName;
            Properties.Settings.Default.CalcFileName = CalcFileName;
            Properties.Settings.Default.CalcItemName = CalcItemName;
            Properties.Settings.Default.ShowUserInterface = IsShowUserInterfaceEnabled;
            Properties.Settings.Default.CreateOutputRtf = IsCreateOutputRtfEnabled;
            Properties.Settings.Default.Save();
        }
        #endregion

        #region "Properties"

        /// <summary>
        /// Full path of input variables XML file
        /// </summary>
        public string InputVariablesFileName
        {
            get { return _inputVariablesFileNameTextBox.Text; }
            set { _inputVariablesFileNameTextBox.Text = value; }
        }
        /// <summary>
        /// Full path of the Calc Library file which contains the Calc Item to calculate
        /// </summary>
        public string CalcFileName
        {
            get { return _calcItemFileTextBox.Text; }
            set { _calcItemFileTextBox.Text = value; }
        }
        /// <summary>
        /// Short name of the Calc Item to calculate
        /// </summary>
        public string CalcItemName
        {
            get { return _calcItemNameTextBox.Text; }
            set { _calcItemNameTextBox.Text = value; }
        }
        /// <summary>
        /// Short name of the Calc Item to calculate with special characters encoded
        /// </summary>
        public string CalcItemNameEncoded =>
            //Semicolons exist in some old calculation item names, these must be encoded because the semicolon is the expression delimiter
            _calcItemNameTextBox.Text.Replace(";", "\\;");
        /// <summary>
        /// Input variables for the calculation in the Tedds variables XML file format 
        /// </summary>
        public string InputVariablesXml =>
            File.Exists(InputVariablesFileName) ? File.ReadAllText(InputVariablesFileName) : null;
        /// <summary>
        /// Output variables from the last run of the calculation in the Tedds variables XML file format.
        /// </summary>
        public string OutputVariablesXml
        {
            get { return _outputVariablesTextBox.Text; }
            set
            {
                _outputVariablesTextBox.Text = value;
                _buttonSaveAsVariables.IsEnabled = !string.IsNullOrEmpty(OutputVariablesXml);
            }
        }
        /// <summary>
        /// Document output from the last run of the calculation in the RTF format.
        /// </summary>
        public string OutputRtf
        {
            get { return _outputRtf; }
            set
            {
                _outputRtfRichTextBox.Document.Blocks.Clear();
                _outputRtf = value;
                if (!string.IsNullOrEmpty(_outputRtf))
                {
                    using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(value)))
                        _outputRtfRichTextBox.Selection.Load(stream, DataFormats.Rtf);
                }

                //Enable/Disable SaveAs buttons
                _buttonSaveAsRtf.IsEnabled = _buttonSaveAsPdf.IsEnabled =
                    _buttonSaveAsTed.IsEnabled = !string.IsNullOrEmpty(_outputRtf);
            }
        }
        /// <summary>
        /// Document output from the last run of the calculation in the PDF format.
        /// </summary>
        public string OutputPdf { get; set; }
        /// <summary>
        /// Status text which is used to feed back information to the user of the application
        /// </summary>
        public string StatusText
        {
            get { return _statusTextLabel.Text; }
            set
            {
                SetStatus(value);
            }
        }
        /// <summary>
        /// Calculating option which determines whether the user interface of the calculation is show or hidden
        /// </summary>
        public bool IsShowUserInterfaceEnabled
        {
            get { return _showUserInterfaceCheckBox.IsChecked == true; }
            set { _showUserInterfaceCheckBox.IsChecked = value; }
        }
        /// <summary>
        /// Calculating option which determines whether the document RTF is produced or not
        /// </summary>
        public bool IsCreateOutputRtfEnabled
        {
            get { return _createOutputRtfCheckBox.IsChecked == true; }
            set { _createOutputRtfCheckBox.IsChecked = value; }
        }
        /// <summary>
        /// Are calculating progress events enabled
        /// </summary>
        public bool CalculatingProgressEvents
        {
            get { return _enableCalculatingProgressEvents.IsChecked == true; }
        }
        /// <summary>
        /// Are undefined variable events enabled
        /// </summary>
        public bool UndefinedVariableEvents
        {
            get { return _enableUndefinedVariableEvents.IsChecked == true; }
        }
        /// <summary>
        /// Are error events enabled
        /// </summary>
        public bool ErrorEvents
        {
            get { return _enableErrorEvents.IsChecked == true;  }
        }
        /// <summary>
        /// Is asynchronous calculating required
        /// </summary>
        public bool IsAsyncCalculatingRequired
        {
            //If any event listening options are enabled then the calculation process must be executed asynchronously 
            //so that the UI thread can be responsive to those events
            get
            {
                return (CalculatingProgressEvents || UndefinedVariableEvents || ErrorEvents);
            }
        }

        #endregion

        #region General methods
        /// <summary>
        /// Initialize a calculator instance to listen to specific events according to the event options.
        /// </summary>
        /// <param name="calculator">Calculator instance to initialize</param>
        private void ConnectEvents(ref Calculator calculator, CalculationData data)
        {
            if (data.CalculatingProgressEvents)
            {
                calculator.CalculatingProgress += CalculatingProgress;
                calculator.CalculatingProgressEvents = true;
            }
            if (data.UndefinedVariableEvents)
            {
                calculator.UndefinedVariable += UndefinedVariable;
                calculator.UndefinedVariableEvents = true;
            }
            if (data.ErrorEvents)
            {
                calculator.CalculatingError += CalculatingError;
                calculator.CalculatingErrorEvents = true;
            }
        }
        /// <summary>
        /// Disable the user interface
        /// </summary>
        public void DisableUI()
        {
            //Use invoker so that code will work synchronously or asynchronously
            Dispatcher.Invoke(() =>
            {
                IsEnabled = false;
            });
        }
        /// <summary>
        /// Enable the user interface
        /// </summary>
        public void EnableUI()
        {
            //Use invoker so that code will work synchronously or asynchronously
            Dispatcher.Invoke(() =>
            {
                IsEnabled = true;
                Activate();
            });
        }
        /// <summary>
        /// Set the status information on the window
        /// </summary>
        /// <param name="text">Text to show on the window</param>
        /// <param name="tooltip">Tool tip text</param>
        public void SetStatus(string text, string tooltip = null)
        {
            if (CheckAccess())
                SetStatus();
            else
                Dispatcher.Invoke(SetStatus);

            void SetStatus()
            {
                _statusTextLabel.Text = text;
                _statusTextLabel.ToolTip = tooltip;
            }
        }
        /// <summary>
        /// Set the status from an exception
        /// </summary>
        /// <param name="ex">Exception</param>
        public void SetStatus(Exception ex)
        {
            SetStatus( $"Exception occurred: {ex.Message}", ex.ToString());
        }
        #endregion

        #region "Private members"

        private string _outputRtf;

        private const string SaveXmlFilter = "XML file (*.xml)|*.xml";
        private const string SaveRtfFilter = "Rich Text Format (*.rtf)|*.rtf";
        private const string SavePdfFilter = "PDF (*.pdf)|*.pdf";
        private const string SaveTedFilter = "Tedds Document (*.ted)|*.ted";

        private const string OpenAllSuffix = "|All files (*.*)|*.*";
        private const string OpenXmlFilter = SaveXmlFilter + OpenAllSuffix;

        private const int DialogResultCancel = 0;
        private const string DefaultSetTitle = "Select Calc Set";
        private const string DefaultLibraryTitle = "Select Calc Library";
        private const string DefaultItemTitle = "Select Calc Item";

        private const string SysPrefix = "$(SysLbrDir)";
        private const string UserPrefix = "$(UserLbrDir)";

        #endregion
    }
}