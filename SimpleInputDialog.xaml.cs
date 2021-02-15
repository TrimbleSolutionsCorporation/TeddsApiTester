using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TeddsAPITester
{
    /// <summary>
    /// Interaction logic for SimpleInputDialog.xaml
    /// </summary>
    public partial class SimpleInputDialog : Window
    {
        public SimpleInputDialog()
        {
            InitializeComponent();
        }
        public string Description
        {
            get { return DescriptionTextBlock.Text; }
            set { DescriptionTextBlock.Text = value; }
        }
        public string Input
        {
            get { return InputTextBox.Text; }
            set { InputTextBox.Text = value; }
        }

        private void OKButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
