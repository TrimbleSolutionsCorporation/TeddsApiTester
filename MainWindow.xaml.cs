using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;

using Microsoft.Win32;

using Tekla.Structural.InteropAssemblies.TeddsCalc;

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
        /// <param name="inputVariablesXml">Input variables for the calculation in the Tedds variables xml file format. Typically created as the output from a previous run of the calculation. Can be null or empty string.</param>
        /// <param name="calcFileName">Full path of the Calc Library file which contains the Calc Item to calculate.</param>
        /// <param name="calcItemName">Short name of the Calc Item to calculate.</param>
        /// <param name="showUserInterface">Determines whether the user interface of the calcualtion is show or hidden.</param>
        /// <param name="outputVariablesXml">Returns all the calculated variables in the Tedds variables xml file format.</param>
        public void CalculateNoOutputRtf(string inputVariablesXml, string calcFileName, string calcItemName,
            bool showUserInterface, out string outputVariablesXml)
        {
            //Create calculator instance and initialize with input
            Calculator calculator = new Calculator();
            calculator.Initialize(null, inputVariablesXml);

            //Apply additional settings/variables
            calculator.Functions.SetVar("_CalcUI", showUserInterface ? 1 : 0);

            //Calculate calculation
            calculator.Functions.Eval($"EvalCalcItem( \"{calcFileName}\", \"{calcItemName}\" )");

            //Get output variables
            outputVariablesXml = calculator.GetVariables();
        }

        /// <summary>
        /// Execute a calculation using the specified optional input variables and return both 
        /// the output variables xml and the output document RTF.
        /// </summary>
        /// <param name="inputVariablesXml">Input variables for the calculation in the Tedds variables xml file format. Typically created as the output from a previous run of the calculation. Can be null or empty string.</param>
        /// <param name="calcFileName">Full path of the Calc Library file which contains the Calc Item to calculate.</param>
        /// <param name="calcItemName">Short name of the Calc Item to calculate.</param>
        /// <param name="showUserInterface">Determines whether the user interface of the calcualtion is show or hidden.</param>
        /// <param name="outputVariablesXml">Returns all the calculated variables in the Tedds variables xml file format.</param>
        /// <param name="outputRtf">Returns the document output of the calculation in the RTF format.</param>
        public void Calculate(string inputVariablesXml, string calcFileName, string calcItemName,
            bool showUserInterface, out string outputVariablesXml, out string outputRtf)
        {
            //Create first calculator instance which is only required for retrieving RTF and getting modified input variables
            Calculator calculator = new Calculator();

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
            calculator2.Initialize(inputRtf, inputVariablesXml);

            //Retrieve output
            outputVariablesXml = calculator2.GetVariables();
            outputRtf = calculator2.GetOutput();                
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
            OutputRTF = OutputVariablesXML = null;
            try
            { 
                string outputVariablesXml;
                if (IsCreateOutputRTFEnabled)
                {
                    string outputRtf;
                    Calculate(InputVariablesXml, CalcFileName, CalcItemNameEncoded,
                        IsShowUserInterfaceEnabled, out outputVariablesXml, out outputRtf);
                    OutputRTF = outputRtf;
                }
                else
                {
                    CalculateNoOutputRtf(InputVariablesXml, CalcFileName, CalcItemNameEncoded,
                        IsShowUserInterfaceEnabled, out outputVariablesXml);
                }            
                OutputVariablesXML = outputVariablesXml;
                StatusText = "...finished Calculating";
            }        
            catch (COMException ex)
            {
                StatusText = $"Exception occured: {ex.Message}";
            }    
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
            OpenFileDialog fileDialog = new OpenFileDialog()
            {
                FileName = InputVariablesFileName,
                Filter = XmlFilter
            };

            if (fileDialog.ShowDialog(this) == true)
                InputVariablesFileName = fileDialog.FileName;
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
            SaveFileDialog saveDialog = new SaveFileDialog()
            { Filter = XmlFilter };

            if (saveDialog.ShowDialog(this) == true)
                File.WriteAllText(saveDialog.FileName, OutputVariablesXML);
        }
        /// <summary>
        /// Save output RTF button event handler. Brose for location to save calculation output 
        /// text to as a Rich Text File (RTF).
        /// </summary>
        /// <param name="sender">Sender of event</param>
        /// <param name="e">Event arguments</param>
        private void OnSaveAsOutputRtfClick(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog()
            { Filter = RtfFilter };

            if (saveDialog.ShowDialog(this) == true)
                File.WriteAllText(saveDialog.FileName, OutputRTF);
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
            IsCreateOutputRTFEnabled = Properties.Settings.Default.CreateOutputRtf;
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
            Properties.Settings.Default.CreateOutputRtf = IsCreateOutputRTFEnabled;
            Properties.Settings.Default.Save();
        }
        #endregion

        #region "Properties"

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
        public string OutputVariablesXML
        {
            get { return _outputVariablesTextBox.Text; }
            set { _outputVariablesTextBox.Text = value; }
        }
        /// <summary>
        /// Document output from the last run of the calculation in the RTF format.
        /// </summary>
        public string OutputRTF
        {
            get
            {
                return new TextRange(
                    _outputRtftRichTextBox.Document.ContentStart,
                    _outputRtftRichTextBox.Document.ContentEnd).Text;
            }
            set
            {
                _outputRtftRichTextBox.Document.Blocks.Clear();
                if (!string.IsNullOrEmpty(value))
                {
                    using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(value)))
                        _outputRtftRichTextBox.Selection.Load(stream, DataFormats.Rtf);
                }
            }
        }
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
        public bool IsCreateOutputRTFEnabled
        {
            get { return _createOutputRtfCheckBox.IsChecked == true; }
            set { _createOutputRtfCheckBox.IsChecked = value; }
        }

        private const string XmlFilter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
        private const string RtfFilter = "RTF files (*.rtf)|*.rtf|All files (*.*)|*.*";

        #endregion       
    }
}