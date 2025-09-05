using System;
using System.Drawing;
using System.Windows.Forms;

namespace StudentManager
{
    /// <summary>
    /// Form for displaying detailed student information in read-only mode
    /// </summary>
    public partial class StudentDetailsForm : Form
    {
        private readonly Student _student;

        public StudentDetailsForm(Student student)
        {
            InitializeComponent();
            _student = student ?? throw new ArgumentNullException(nameof(student));
            
            InitializeForm();
            LoadStudentDetails();
        }

        private void InitializeForm()
        {
            this.Text = $"Student Details - {_student.Name}";
            this.Size = new Size(500, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            CreateLayout();
        }

        private void CreateLayout()
        {
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 12,
                ColumnCount = 2,
                Padding = new Padding(20),
                BackColor = Color.WhiteSmoke
            };

            // Configure column styles
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));

            // Configure row styles
            for (int i = 0; i < 12; i++)
            {
                mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            }

            // Add title
            var titleLabel = new Label
            {
                Text = "Student Information",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            mainPanel.Controls.Add(titleLabel, 0, 0);
            mainPanel.SetColumnSpan(titleLabel, 2);

            // Add separator
            var separator = new Panel
            {
                Height = 2,
                BackColor = Color.DarkBlue,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 10)
            };
            mainPanel.Controls.Add(separator, 0, 1);
            mainPanel.SetColumnSpan(separator, 2);

            // Add detail fields
            var fields = new[]
            {
                ("Student ID:", _student.Id.ToString()),
                ("Full Name:", _student.Name),
                ("Age:", $"{_student.Age} years old ({_student.GetAgeGroup()})"),
                ("Department:", _student.Department),
                ("Email Address:", string.IsNullOrEmpty(_student.Email) ? "Not provided" : _student.Email),
                ("Phone Number:", string.IsNullOrEmpty(_student.PhoneNumber) ? "Not provided" : _student.PhoneNumber),
                ("Enrollment Date:", _student.EnrollmentDate.ToString("MMMM dd, yyyy")),
                ("GPA:", $"{_student.GPA:F2} ({_student.GetLetterGrade()})"),
                ("Status:", _student.IsActive ? "Active" : "Inactive"),
                ("Account Created:", _student.CreatedDate.ToString("MMMM dd, yyyy 'at' hh:mm tt")),
                ("Last Modified:", _student.ModifiedDate.ToString("MMMM dd, yyyy 'at' hh:mm tt"))
            };

            for (int i = 0; i < fields.Length; i++)
            {
                // Label
                var label = new Label
                {
                    Text = fields[i].Item1,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.DarkSlateGray,
                    TextAlign = ContentAlignment.MiddleRight,
                    Dock = DockStyle.Fill
                };
                mainPanel.Controls.Add(label, 0, i + 2);

                // Value
                var value = new Label
                {
                    Text = fields[i].Item2,
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                    ForeColor = Color.Black,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Dock = DockStyle.Fill,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Padding = new Padding(5, 0, 0, 0)
                };

                // Special formatting for certain fields
                if (fields[i].Item1.Contains("GPA"))
                {
                    if (_student.GPA >= 3.5m)
                        value.ForeColor = Color.Green;
                    else if (_student.GPA >= 2.5m)
                        value.ForeColor = Color.Orange;
                    else
                        value.ForeColor = Color.Red;
                }
                else if (fields[i].Item1.Contains("Status"))
                {
                    value.ForeColor = _student.IsActive ? Color.Green : Color.Red;
                    value.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }

                mainPanel.Controls.Add(value, 1, i + 2);
            }

            // Add close button
            var closeButton = new Button
            {
                Text = "Close",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 35),
                Anchor = AnchorStyles.None
            };

            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) => this.Close();

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 50
            };
            buttonPanel.Controls.Add(closeButton);
            closeButton.Location = new Point((buttonPanel.Width - closeButton.Width) / 2, 10);

            mainPanel.Controls.Add(buttonPanel, 0, fields.Length + 2);
            mainPanel.SetColumnSpan(buttonPanel, 2);

            this.Controls.Add(mainPanel);
        }

        private void LoadStudentDetails()
        {
            // Details are loaded during form initialization in CreateLayout()
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // StudentDetailsForm
            // 
            this.ClientSize = new System.Drawing.Size(484, 561);
            this.Name = "StudentDetailsForm";
            this.ResumeLayout(false);
        }
    }
}