////////////////////////////////
///ProgressbarForm.cs - version 0.04052006.1rc
///BY DOWNLOADING AND USING, YOU AGREE TO THE FOLLOWING TERMS:
///Copyright (c) 2006 by Joseph P. Socoloski III
///LICENSE
///If it is your intent to use this software for non-commercial purposes, 
///such as in academic research, this software is free and is covered under 
///the GNU GPL License, given here: <http://www.gnu.org/licenses/gpl.txt> 
using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Web;
using System.IO;
using System.Net;
using System.Text;
using System.Configuration;
using System.Runtime.InteropServices;

namespace SLMIV.Utils
{
    namespace Forms
    {
        /// <summary>
        /// 
        /// </summary>
        public partial class ProgressbarForm : Form
        {
            private int Max;

            /// <summary>
            /// The maximum steps to be performed by the progressbar.
            /// </summary>
            public int MaximumSteps
            {
                get
                {
                    if (Max == -1)
                        Max = 0;

                    return Max;
                }
                set
                {
                    Max = value;
                }
            }
            /// <summary>
            /// 
            /// </summary>
            public ProgressbarForm()
            {
                InitializeComponent();
            }

            private void ProgressbarForm_Load(object sender, EventArgs e)
            {
                // Display the ProgressBar control.
                pBar1.Visible = true;
                // Set Minimum to 1 to represent the first file being copied.
                pBar1.Minimum = 1;
                // Set Maximum to the total number of steps to perform.
                pBar1.Maximum = MaximumSteps;
                // Set the initial value of the ProgressBar.
                pBar1.Value = 1;
                // Set the Step property to a value of 1 to represent each file being copied.
                pBar1.Step = 1;
            }

            /// <summary>
            /// Call PerformStep()
            /// </summary>
            /// <param name="message">Message to be displayed in the ProgressbarForm.</param>
            /// <param name="number">int to be passed (not currently used).</param>
            public void Step(string message, int number)
            {
                if (this.pBar1.Style == ProgressBarStyle.Blocks)
                {
                    this.Focus();
                    lbprogress.Text = message;
                    lbprogress.Update();
                    this.Focus();
                    pBar1.PerformStep();
                    this.Update();
                }
                if (this.pBar1.Style == ProgressBarStyle.Marquee)
                {
                    //pBar1.Visible = true;
                    this.Focus();
                    pBar1.Visible = true;
                    this.Update();
                }
            }


        }
    }
}