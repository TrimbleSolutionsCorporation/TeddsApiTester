#define SavePdfViaTeddsApplication

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;

using Microsoft.Win32;

using Tekla.Structural.InteropAssemblies.Tedds;
using Tekla.Structural.InteropAssemblies.TeddsCalc;

using Application = Tekla.Structural.InteropAssemblies.Tedds.Application;

namespace TeddsAPITester
{
    /// <summary>
    /// The Tedds API Tester is designed for testing or learning how to use the Tedds API, it can be used to calculate an existing 
    /// Tedds calculation which is stored in a Tedds Calc Library. The output variables produced by running a 
    /// calcaultion can be saved and then used as the input for a subsequent run of the calculation. Typically you would 
    /// run the calculation once with the user interface enabled allowing you to setup the default input for a 
    /// design. Subsequent calculations can then be started using this default input with further changes 
    /// applied to the input in your own code in order to automate a design workflow. 
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
        /// the output variables xml. Omitting the RTF output when it is not needed will improve performance.
        /// </summary>
        /// <param name="userName">If a Tekla online license is being used then this is the login user name for the Trimble Identity account to use.</param>
        /// <param name="password">If a Tekla online license is being used then this is the login password for the Trimble Identity account to use</param>
        /// <param name="inputVariablesXml">Input variables for the calculation in the Tedds variables xml file format.
        /// Typically created as the output from a previous run of the calculation. Can be null or empty string.</param>
        /// <param name="calcFileName">Full path of the Calc Library file which contains the Calc Item to calculate.</param>
        /// <param name="calcItemName">Short name of the Calc Item to calculate.</param>
        /// <param name="showUserInterface">Determines whether the user interface of the calcualtion is show or hidden.</param>
        /// <returns>Returns all the calculated variables in the Tedds variables xml file format.</returns>
        public string CalculateNoOutputRtf(string userName, string password, string inputVariablesXml, string calcFileName, string calcItemName, bool showUserInterface)
        {
            //Create calculator instance and initialize with input
            Calculator calculator = new Calculator();
            User32Native.SetForegroundWindow((IntPtr)calculator.WindowHandle);

            //If online license login is required then login
            if (!(string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(password)))
                calculator.Login(userName, password);

            try
            {
                this.IsEnabled = false;

                calculator.Initialize(null, inputVariablesXml);

                //Apply additional settings/variables
                calculator.Functions.SetVar("_CalcUI", showUserInterface ? 1 : 0);

                //Calculate calculation
                calculator.Functions.Eval($"EvalCalcItem( \"{calcFileName}\", \"{calcItemName}\" )");

                //Get output variables
                return calculator.GetVariables();
            }
            finally
            {
                this.IsEnabled = true;
                this.Activate();
            }
        }

        /// <summary>
        /// Execute a calculation using the specified optional input variables and return both 
        /// the output variables xml and the output document RTF.
        /// </summary>
        /// <param name="userName">If a Tekla online license is being used then this is the login user name for the Trimble Identity account to use.</param>
        /// <param name="password">If a Tekla online license is being used then this is the login password for the Trimble Identity account to use</param>
        /// <param name="inputVariablesXml">Input variables for the calculation in the Tedds variables xml file format.
        /// Typically created as the output from a previous run of the calculation. Can be null or empty string.</param>
        /// <param name="calcFileName">Full path of the Calc Library file which contains the Calc Item to calculate.</param>
        /// <param name="calcItemName">Short name of the Calc Item to calculate.</param>
        /// <param name="showUserInterface">Determines whether the user interface of the calcualtion is show or hidden.</param>
        /// <param name="outputVariablesXml">Returns all the calculated variables in the Tedds variables xml file format.</param>
        /// <param name="outputRtf">Returns the document output of the calculation in the RTF format.</param>
        /// <param name="outputPdf">Returns the document output of the calculation in the PDF format.</param>
        public void Calculate(string userName, string password, string inputVariablesXml, string calcFileName, string calcItemName,
            bool showUserInterface, out string outputVariablesXml, out string outputRtf, out string outputPdf)
        {
            outputVariablesXml = outputRtf = outputPdf = null;

            //Create first calculator instance which is only required for retrieving RTF and getting modified input variables
            Calculator calculator = new Calculator();

            //If online license login is required then login
            if (!(string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(password)))
                calculator.Login(userName, password);

            if (!showUserInterface)
            {
                //Apply additional settings/variables to existing input
                calculator.Initialize(null, inputVariablesXml);
                calculator.Functions.SetVar("_CalcUI", 0);
                inputVariablesXml = calculator.GetVariables();
            }
            else
            {
                calculator.Initialize(null, null);
            }

            //Use Tedds function "GetCalcItemText" to get RTF Input/Output for calculation
            ICalcValue calcItemRtf = calculator.Functions.Eval($"GetCalcItemText( \"{calcFileName}\", \"{calcItemName}\" )");
            //Decode Tedds string to correctly formatted RTF
            string inputRtf = calcItemRtf.ToString().Replace("\\\"", "\"").Replace("\\;", ";");

            //Initialize second calculator but this time with input/output RTF and input variables
            Calculator calculator2 = new Calculator();
            User32Native.SetForegroundWindow((IntPtr)calculator2.WindowHandle);

            try
            {
                this.IsEnabled = false;
                calculator2.Initialize(inputRtf, inputVariablesXml);

                if (calculator2.Status == CalcStatus.Ok ||
                    calculator2.Status == CalcStatus.Interrupted)
                {
                    //Retrieve output
                    outputVariablesXml = calculator2.GetVariables();
                    outputRtf = calculator2.GetOutput(OutputFormat.Rtf);
                    outputPdf = calculator2.GetOutput(OutputFormat.Pdf);
                }
            }
            finally
            {
                this.IsEnabled = true;
                this.Activate();
            }
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
        /// Using the Tedds application create a pdf file.
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
            catch (COMException ex)
            {
                StatusText = $"Exception occured: {ex.Message}";
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
        /// <param name="dialogTitle">Title to show on browse dilaog</param>
        /// <param name="saveLibrary">Bool specifying whether to save library</param>
        /// <param name="systemDirectories">Bool specifying whether to look for calcs in system directory</param>
        /// <param name="userDirectories">Bool specifying whether to look for calcs in user directory</param>
        /// <param name="usePlaceholders">Bool specifying whether to use place holders in path</param>
        /// <returns>True if a Calc Library was selected, false if the browse dialog was cancelled</returns>
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
        /// <param name="dialogTitle">Title to show on browse dilaog</param>
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
            //Very basic input validation
            if (!string.IsNullOrEmpty(InputVariablesFileName) && !File.Exists(InputVariablesFileName))
            {
                StatusText = $"Select a valid input file, '{InputVariablesFileName}' does not exist.";
                return;
            }
            if (string.IsNullOrEmpty(CalcFileName) || string.IsNullOrEmpty(CalcItemName))
            {
                StatusText = "Enter the libray file name and the item name of the calculation you want to calculate";
                return;
            }

            StatusText = $"Started calculating {CalcItemName}...";
            OutputRtf = OutputVariablesXml = null;
            try
            {
                string outputVariablesXml;
                if (IsCreateOutputRtfEnabled)
                {
                    Calculate(UserName, Password, InputVariablesXml, CalcFileName, CalcItemNameEncoded,
                        IsShowUserInterfaceEnabled, out outputVariablesXml, out string outputRtf, out string outputPdf);
                    OutputRtf = outputRtf;
                    OutputPdf = outputPdf;
                }
                else
                {
                    outputVariablesXml = CalculateNoOutputRtf(UserName, Password, InputVariablesXml, CalcFileName, CalcItemNameEncoded,
                        IsShowUserInterfaceEnabled);
                }
                OutputVariablesXml = outputVariablesXml;
                StatusText = "...finished Calculating";
            }
            catch (COMException ex)
            {
                StatusText = $"Exception occured: {ex.Message}";
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
        /// Browse for location to save calcualtion ouptut variables to as an xml file which can 
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
        /// Login user name for the Trimble Identity account to use
        /// </summary>
        public string UserName =>
            _userNameTextBox.Text;
        /// <summary>
        /// Login password for the Trimble Identity account to use
        /// </summary>
        public string Password =>
            _passwordTextBox.Password;

        /// <summary>
        /// Full path of input variables xml file
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
        /// Input variables for the calculation in the Tedds variables xml file format 
        /// </summary>
        public string InputVariablesXml =>
            File.Exists(InputVariablesFileName) ? File.ReadAllText(InputVariablesFileName) : null;
        /// <summary>
        /// Output variables from the last run of the calculation in the Tedds variables xml file format.
        /// </summary>
        public string OutputVariablesXml
        {
            get { return _outputVariablesTextBox.Text; }
            set { _outputVariablesTextBox.Text = value; }
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
                _statusTextLabel.Text = value;
                //Force an update of the status text
                Dispatcher.Invoke(DispatcherPriority.Input, new Action(() => { }));
            }
        }
        /// <summary>
        /// Calculating option which determines whether the user interface of the calcualtion is show or hidden
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