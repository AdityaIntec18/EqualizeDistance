using System;
using System.Windows.Forms;

namespace EqualizePipes
{
    public partial class Form1 : Form
    {
        public double Distance { get; private set; }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            label1.Text = "Enter the distance between pipes (in meters):";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (double.TryParse(textBox1.Text, out double distance) && distance > 0)
            {
                Distance = distance;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid positive number.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            Close();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
