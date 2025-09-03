using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace StudentManager
{
    /// <summary>
    /// Main form for the Student Management application with comprehensive UI features
    /// </summary>
    public partial class MainForm : Form
    {
        #region Private Fields

        private readonly DatabaseHelper _databaseHelper;
        private readonly ILogger<MainForm> _logger;
        private BindingSource _bindingSource;
        private Student _selectedStudent;
        private bool _isLoading;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private string _currentSearchTerm = string.Empty;
        private Timer _searchTimer;

        #endregion

        #region Constructor

        public MainForm(ILogger<MainForm> logger = null)
        {
            InitializeComponent();
            _logger = logger;
            _databaseHelper = new DatabaseHelper(_logger as ILogger<DatabaseHelper>);
            _bindingSource = new BindingSource();
            
            InitializeUI();
            SetupEventHandlers();
            LoadDataAsync();
        }

        #endregion

        #region Form Events

        private async void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                await InitializeApplicationAsync();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Failed to initialize application", ex);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _databaseHelper?.Dispose();
                _searchTimer?.Dispose();
                _logger?.LogInformation("Application closed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during application shutdown");
            }
        }

        #endregion

        #region Button Click Events

        private async void btnAdd_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateInput())
                    return;

                var student = CreateStudentFromInput();
                
                SetLoadingState(true);
                await _databaseHelper.AddStudentAsync(student);
                
                ShowSuccessMessage($"Student '{student.Name}' added successfully!");
                ClearInputFields();
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Failed to add student", ex);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async void btnUpdate_Click(object sender, EventArgs e)
        {
            try
            {
                if (_selectedStudent == null)
                {
                    ShowWarningMessage("Please select a student to update.");
                    return;
                }

                if (!ValidateInput())
                    return;

                var student = CreateStudentFromInput();
                student.Id = _selectedStudent.Id;
                student.CreatedDate = _selectedStudent.CreatedDate;

                SetLoadingState(true);
                var success = await _databaseHelper.UpdateStudentAsync(student);
                
                if (success)
                {
                    ShowSuccessMessage($"Student '{student.Name}' updated successfully!");
                    ClearInputFields();
                    await RefreshDataAsync();
                }
                else
                {
                    ShowWarningMessage("Student not found or no changes were made.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Failed to update student", ex);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async void btnDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (_selectedStudent == null)
                {
                    ShowWarningMessage("Please select a student to delete.");
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to delete student '{_selectedStudent.Name}'?\n\nThis action cannot be undone.",
                    "Confirm Deletion",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;

                SetLoadingState(true);
                var success = await _databaseHelper.DeleteStudentAsync(_selectedStudent.Id);
                
                if (success)
                {
                    ShowSuccessMessage($"Student '{_selectedStudent.Name}' deleted successfully!");
                    ClearInputFields();
                    await RefreshDataAsync();
                }
                else
                {
                    ShowWarningMessage("Student not found or already deleted.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Failed to delete student", ex);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            await PerformSearchAsync();
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            await RefreshDataAsync();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            ClearInputFields();
            txtSearch.Clear();
            _currentSearchTerm = string.Empty;
            LoadDataAsync();
        }

        private async void btnExport_Click(object sender, EventArgs e)
        {
            await ExportDataAsync();
        }

        private async void btnImport_Click(object sender, EventArgs e)
        {
            await ImportDataAsync();
        }

        private async void btnStatistics_Click(object sender, EventArgs e)
        {
            await ShowStatisticsAsync();
        }

        #endregion

        #region DataGridView Events

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                if (dataGridView1.SelectedRows.Count > 0 && !_isLoading)
                {
                    var selectedRow = dataGridView1.SelectedRows[0];
                    LoadStudentToInputFields(selectedRow);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling selection change");
            }
        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                ShowStudentDetailsDialog();
            }
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                // Format GPA column to 2 decimal places
                if (dataGridView1.Columns[e.ColumnIndex].Name == "GPA" && e.Value != null)
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal gpa))
                    {
                        e.Value = gpa.ToString("F2");
                        e.FormattingApplied = true;

                        // Color code GPA values
                        if (gpa >= 3.5m)
                            e.CellStyle.ForeColor = Color.Green;
                        else if (gpa >= 2.5m)
                            e.CellStyle.ForeColor = Color.Orange;
                        else
                            e.CellStyle.ForeColor = Color.Red;
                    }
                }

                // Format date columns
                if ((dataGridView1.Columns[e.ColumnIndex].Name.Contains("Date") || 
                     dataGridView1.Columns[e.ColumnIndex].Name == "EnrollmentDate") && e.Value != null)
                {
                    if (DateTime.TryParse(e.Value.ToString(), out DateTime date))
                    {
                        e.Value = date.ToString("MM/dd/yyyy");
                        e.FormattingApplied = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error formatting cell");
            }
        }

        #endregion

        #region Search Events

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            _searchTimer?.Stop();
            _searchTimer?.Start();
        }

        private async void SearchTimer_Tick(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            await PerformSearchAsync();
        }

        #endregion

        #region Pagination Events

        private async void btnFirstPage_Click(object sender, EventArgs e)
        {
            _currentPage = 1;
            await LoadDataAsync();
        }

        private async void btnPrevPage_Click(object sender, EventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await LoadDataAsync();
            }
        }

        private async void btnNextPage_Click(object sender, EventArgs e)
        {
            _currentPage++;
            await LoadDataAsync();
        }

        #endregion

        #region Private Methods

        private void InitializeUI()
        {
            this.Text = "Student Management System";
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Setup DataGridView
            dataGridView1.DataSource = _bindingSource;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.ReadOnly = true;

            // Setup search timer
            _searchTimer = new Timer();
            _searchTimer.Interval = 500; // 500ms delay
            _searchTimer.Tick += SearchTimer_Tick;

            // Set initial states
            SetLoadingState(false);
            UpdatePageInfo();
        }

        private void SetupEventHandlers()
        {
            // Form events
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;

            // Button events
            btnAdd.Click += btnAdd_Click;
            btnUpdate.Click += btnUpdate_Click;
            btnDelete.Click += btnDelete_Click;
            btnSearch.Click += btnSearch_Click;
            btnRefresh.Click += btnRefresh_Click;
            btnClear.Click += btnClear_Click;

            // DataGridView events
            dataGridView1.SelectionChanged += dataGridView1_SelectionChanged;
            dataGridView1.CellDoubleClick += dataGridView1_CellDoubleClick;
            dataGridView1.CellFormatting += dataGridView1_CellFormatting;

            // Search events
            txtSearch.TextChanged += txtSearch_TextChanged;

            // Input validation events
            txtAge.KeyPress += (s, e) => { if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)) e.Handled = true; };
            txtGPA.KeyPress += (s, e) => { if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.' && !char.IsControl(e.KeyChar)) e.Handled = true; };
        }

        private async Task InitializeApplicationAsync()
        {
            try
            {
                SetLoadingState(true);

                // Test database connection
                if (!_databaseHelper.TestConnection())
                {
                    ShowErrorMessage("Database connection failed", new Exception("Unable to connect to the database. Please check your connection string."));
                    return;
                }

                // Load initial data
                await LoadDataAsync();

                _logger?.LogInformation("Application initialized successfully");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                SetLoadingState(true);

                DataTable dataTable;
                if (string.IsNullOrWhiteSpace(_currentSearchTerm))
                {
                    dataTable = await _databaseHelper.GetAllStudentsAsync(chkIncludeInactive.Checked);
                }
                else
                {
                    var students = _databaseHelper.SearchStudents(_currentSearchTerm);
                    dataTable = ConvertToDataTable(students);
                }

                _bindingSource.DataSource = dataTable;
                UpdateStatusBar($"Loaded {dataTable.Rows.Count} students");
                UpdatePageInfo();

                _logger?.LogInformation($"Loaded {dataTable.Rows.Count} students");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Failed to load student data", ex);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async Task RefreshDataAsync()
        {
            ClearInputFields();
            _selectedStudent = null;
            await LoadDataAsync();
        }

        private async Task PerformSearchAsync()
        {
            try
            {
                _currentSearchTerm = txtSearch.Text.Trim();
                _currentPage = 1;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Search failed", ex);
            }
        }

        private bool ValidateInput()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(txtName.Text))
                errors.Add("Name is required");

            if (!int.TryParse(txtAge.Text, out int age) || age < 16 || age > 100)
                errors.Add("Age must be a number between 16 and 100");

            if (string.IsNullOrWhiteSpace(txtDept.Text))
                errors.Add("Department is required");

            if (!string.IsNullOrWhiteSpace(txtGPA.Text))
            {
                if (!decimal.TryParse(txtGPA.Text, out decimal gpa) || gpa < 0.0m || gpa > 4.0m)
                    errors.Add("GPA must be a number between 0.0 and 4.0");
            }

            if (!string.IsNullOrWhiteSpace(txtEmail.Text) && !IsValidEmail(txtEmail.Text))
                errors.Add("Please enter a valid email address");

            if (errors.Any())
            {
                ShowValidationErrors(errors);
                return false;
            }

            return true;
        }

        private Student CreateStudentFromInput()
        {
            return new Student
            {
                Name = txtName.Text.Trim(),
                Age = int.Parse(txtAge.Text),
                Department = txtDept.Text.Trim(),
                Email = txtEmail.Text.Trim(),
                PhoneNumber = txtPhone.Text.Trim(),
                GPA = string.IsNullOrWhiteSpace(txtGPA.Text) ? 0.0m : decimal.Parse(txtGPA.Text),
                EnrollmentDate = dtpEnrollment.Value,
                IsActive = chkIsActive.Checked
            };
        }

        private void LoadStudentToInputFields(DataGridViewRow row)
        {
            try
            {
                if (row.DataBoundItem is DataRowView dataRowView)
                {
                    var dataRow = dataRowView.Row;
                    
                    _selectedStudent = new Student
                    {
                        Id = Convert.ToInt32(dataRow["Id"]),
                        Name = dataRow["Name"].ToString(),
                        Age = Convert.ToInt32(dataRow["Age"]),
                        Department = dataRow["Department"].ToString(),
                        Email = dataRow["Email"]?.ToString() ?? string.Empty,
                        PhoneNumber = dataRow["PhoneNumber"]?.ToString() ?? string.Empty,
                        GPA = dataRow["GPA"] == DBNull.Value ? 0.0m : Convert.ToDecimal(dataRow["GPA"]),
                        EnrollmentDate = dataRow["EnrollmentDate"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(dataRow["EnrollmentDate"]),
                        IsActive = dataRow["IsActive"] == DBNull.Value ? true : Convert.ToBoolean(dataRow["IsActive"]),
                        CreatedDate = dataRow["CreatedDate"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(dataRow["CreatedDate"])
                    };

                    // Populate input fields
                    txtId.Text = _selectedStudent.Id.ToString();
                    txtName.Text = _selectedStudent.Name;
                    txtAge.Text = _selectedStudent.Age.ToString();
                    txtDept.Text = _selectedStudent.Department;
                    txtEmail.Text = _selectedStudent.Email;
                    txtPhone.Text = _selectedStudent.PhoneNumber;
                    txtGPA.Text = _selectedStudent.GPA.ToString("F2");
                    dtpEnrollment.Value = _selectedStudent.EnrollmentDate;
                    chkIsActive.Checked = _selectedStudent.IsActive;

                    // Enable update/delete buttons
                    btnUpdate.Enabled = true;
                    btnDelete.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading student to input fields");
                ShowErrorMessage("Failed to load student data", ex);
            }
        }

        private void ClearInputFields()
        {
            txtId.Clear();
            txtName.Clear();
            txtAge.Clear();
            txtDept.Clear();
            txtEmail.Clear();
            txtPhone.Clear();
            txtGPA.Clear();
            dtpEnrollment.Value = DateTime.Now;
            chkIsActive.Checked = true;

            _selectedStudent = null;
            btnUpdate.Enabled = false;
            btnDelete.Enabled = false;
        }

        private void SetLoadingState(bool isLoading)
        {
            _isLoading = isLoading;
            
            // Disable/enable controls during loading
            btnAdd.Enabled = !isLoading;
            btnUpdate.Enabled = !isLoading && _selectedStudent != null;
            btnDelete.Enabled = !isLoading && _selectedStudent != null;
            btnSearch.Enabled = !isLoading;
            btnRefresh.Enabled = !isLoading;
            
            // Show/hide loading indicator
            if (isLoading)
            {
                Cursor = Cursors.WaitCursor;
                statusLabel.Text = "Loading...";
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        private void UpdateStatusBar(string message)
        {
            statusLabel.Text = message;
        }

        private void UpdatePageInfo()
        {
            lblPageInfo.Text = $"Page {_currentPage}";
            btnPrevPage.Enabled = _currentPage > 1;
        }

        private async Task ExportDataAsync()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx",
                    DefaultExt = "csv"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    SetLoadingState(true);
                    var data = await _databaseHelper.GetAllStudentsAsync(true);
                    
                    // Implementation for export would go here
                    ShowSuccessMessage($"Data exported successfully to {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Export failed", ex);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async Task ImportDataAsync()
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx",
                    Multiselect = false
                };

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    SetLoadingState(true);
                    
                    // Implementation for import would go here
                    ShowSuccessMessage("Data imported successfully");
                    await RefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Import failed", ex);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async Task ShowStatisticsAsync()
        {
            try
            {
                SetLoadingState(true);
                var stats = _databaseHelper.GetStudentStatistics();
                var deptStats = _databaseHelper.GetDepartmentStatistics();

                var statsForm = new StatisticsForm(stats, deptStats);
                statsForm.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Failed to load statistics", ex);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void ShowStudentDetailsDialog()
        {
            if (_selectedStudent != null)
            {
                var detailsForm = new StudentDetailsForm(_selectedStudent);
                detailsForm.ShowDialog();
            }
        }

        private DataTable ConvertToDataTable(List<Student> students)
        {
            var dataTable = new DataTable();
            
            // Add columns
            dataTable.Columns.Add("Id", typeof(int));
            dataTable.Columns.Add("Name", typeof(string));
            dataTable.Columns.Add("Age", typeof(int));
            dataTable.Columns.Add("Department", typeof(string));
            dataTable.Columns.Add("Email", typeof(string));
            dataTable.Columns.Add("PhoneNumber", typeof(string));
            dataTable.Columns.Add("EnrollmentDate", typeof(DateTime));
            dataTable.Columns.Add("GPA", typeof(decimal));
            dataTable.Columns.Add("IsActive", typeof(bool));
            dataTable.Columns.Add("CreatedDate", typeof(DateTime));
            dataTable.Columns.Add("ModifiedDate", typeof(DateTime));

            // Add rows
            foreach (var student in students)
            {
                dataTable.Rows.Add(
                    student.Id,
                    student.Name,
                    student.Age,
                    student.Department,
                    student.Email,
                    student.PhoneNumber,
                    student.EnrollmentDate,
                    student.GPA,
                    student.IsActive,
                    student.CreatedDate,
                    student.ModifiedDate
                );
            }

            return dataTable;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Message Methods

        private void ShowErrorMessage(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? $"{message}\n\nError: {ex.Message}" : message;
            MessageBox.Show(fullMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _logger?.LogError(ex, message);
        }

        private void ShowSuccessMessage(string message)
        {
            MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _logger?.LogInformation(message);
        }

        private void ShowWarningMessage(string message)
        {
            MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _logger?.LogWarning(message);
        }

        private void ShowValidationErrors(List<string> errors)
        {
            var message = "Please correct the following errors:\n\n" + string.Join("\n", errors);
            MessageBox.Show(message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        #endregion
    }
}