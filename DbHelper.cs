using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Configuration;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace StudentManager
{
    /// <summary>
    /// Data access layer for Student operations with comprehensive error handling and logging
    /// </summary>
    public class DatabaseHelper : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseHelper> _logger;
        private bool _disposed = false;

        /// <summary>
        /// Constructor with default connection string
        /// </summary>
        public DatabaseHelper(ILogger<DatabaseHelper> logger = null)
        {
            _connectionString = ConfigurationManager.ConnectionStrings["StudentDB"]?.ConnectionString 
                ?? "Server=localhost;Database=StudentDB;Trusted_Connection=True;Connection Timeout=30;";
            _logger = logger;
        }

        /// <summary>
        /// Constructor with custom connection string
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="logger">Logger instance</param>
        public DatabaseHelper(string connectionString, ILogger<DatabaseHelper> logger = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger;
        }

        /// <summary>
        /// Tests the database connection
        /// </summary>
        /// <returns>True if connection successful, false otherwise</returns>
        public bool TestConnection()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    _logger?.LogInformation("Database connection test successful");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Database connection test failed");
                return false;
            }
        }

        /// <summary>
        /// Gets all students from the database with optional filtering and pagination
        /// </summary>
        /// <param name="includeInactive">Whether to include inactive students</param>
        /// <param name="pageNumber">Page number for pagination (1-based)</param>
        /// <param name="pageSize">Number of records per page</param>
        /// <returns>DataTable containing student records</returns>
        public DataTable GetAllStudents(bool includeInactive = true, int pageNumber = 1, int pageSize = 100)
        {
            var dataTable = new DataTable();
            
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = @"
                        WITH StudentPages AS (
                            SELECT *, ROW_NUMBER() OVER (ORDER BY Name) as RowNum
                            FROM Students 
                            WHERE (@IncludeInactive = 1 OR IsActive = 1)
                        )
                        SELECT * FROM StudentPages 
                        WHERE RowNum BETWEEN @StartRow AND @EndRow
                        ORDER BY Name";

                    using (var command = new SqlCommand(query, connection))
                    {
                        var startRow = (pageNumber - 1) * pageSize + 1;
                        var endRow = pageNumber * pageSize;

                        command.Parameters.AddWithValue("@IncludeInactive", includeInactive);
                        command.Parameters.AddWithValue("@StartRow", startRow);
                        command.Parameters.AddWithValue("@EndRow", endRow);

                        using (var adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(dataTable);
                        }
                    }
                }

                _logger?.LogInformation($"Retrieved {dataTable.Rows.Count} students (Page {pageNumber})");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving students from database");
                throw new DataException("Failed to retrieve students from database", ex);
            }

            return dataTable;
        }

        /// <summary>
        /// Gets a student by ID
        /// </summary>
        /// <param name="id">Student ID</param>
        /// <returns>Student object or null if not found</returns>
        public Student GetStudentById(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Student ID must be positive", nameof(id));

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = "SELECT * FROM Students WHERE Id = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        connection.Open();

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return MapReaderToStudent(reader);
                            }
                        }
                    }
                }

                _logger?.LogInformation($"Student with ID {id} not found");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error retrieving student with ID {id}");
                throw new DataException($"Failed to retrieve student with ID {id}", ex);
            }
        }

        /// <summary>
        /// Searches students by various criteria
        /// </summary>
        /// <param name="searchTerm">Search term for name or department</param>
        /// <param name="department">Specific department filter</param>
        /// <param name="minAge">Minimum age filter</param>
        /// <param name="maxAge">Maximum age filter</param>
        /// <param name="minGpa">Minimum GPA filter</param>
        /// <returns>List of matching students</returns>
        public List<Student> SearchStudents(string searchTerm = null, string department = null, 
                                          int? minAge = null, int? maxAge = null, decimal? minGpa = null)
        {
            var students = new List<Student>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = @"
                        SELECT * FROM Students 
                        WHERE IsActive = 1
                        AND (@SearchTerm IS NULL OR Name LIKE '%' + @SearchTerm + '%' OR Department LIKE '%' + @SearchTerm + '%')
                        AND (@Department IS NULL OR Department = @Department)
                        AND (@MinAge IS NULL OR Age >= @MinAge)
                        AND (@MaxAge IS NULL OR Age <= @MaxAge)
                        AND (@MinGpa IS NULL OR GPA >= @MinGpa)
                        ORDER BY Name";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SearchTerm", (object)searchTerm ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Department", (object)department ?? DBNull.Value);
                        command.Parameters.AddWithValue("@MinAge", (object)minAge ?? DBNull.Value);
                        command.Parameters.AddWithValue("@MaxAge", (object)maxAge ?? DBNull.Value);
                        command.Parameters.AddWithValue("@MinGpa", (object)minGpa ?? DBNull.Value);

                        connection.Open();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                students.Add(MapReaderToStudent(reader));
                            }
                        }
                    }
                }

                _logger?.LogInformation($"Search returned {students.Count} students");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error searching students");
                throw new DataException("Failed to search students", ex);
            }

            return students;
        }

        /// <summary>
        /// Adds a new student to the database
        /// </summary>
        /// <param name="student">Student object to add</param>
        /// <returns>The ID of the newly created student</returns>
        public int AddStudent(Student student)
        {
            if (student == null)
                throw new ArgumentNullException(nameof(student));

            ValidateStudent(student);

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = @"
                        INSERT INTO Students (Name, Age, Department, Email, PhoneNumber, EnrollmentDate, GPA, IsActive, CreatedDate, ModifiedDate)
                        VALUES (@Name, @Age, @Department, @Email, @PhoneNumber, @EnrollmentDate, @GPA, @IsActive, @CreatedDate, @ModifiedDate);
                        SELECT SCOPE_IDENTITY();";

                    using (var command = new SqlCommand(query, connection))
                    {
                        AddStudentParameters(command, student);
                        connection.Open();

                        var newId = Convert.ToInt32(command.ExecuteScalar());
                        student.Id = newId;

                        _logger?.LogInformation($"Successfully added student with ID {newId}: {student.Name}");
                        return newId;
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 2627) // Unique constraint violation
            {
                _logger?.LogWarning($"Duplicate student entry attempted: {student.Email}");
                throw new InvalidOperationException("A student with this email already exists", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error adding student: {student.Name}");
                throw new DataException("Failed to add student to database", ex);
            }
        }

        /// <summary>
        /// Updates an existing student in the database
        /// </summary>
        /// <param name="student">Student object with updated information</param>
        /// <returns>True if update successful, false if student not found</returns>
        public bool UpdateStudent(Student student)
        {
            if (student == null)
                throw new ArgumentNullException(nameof(student));
            
            if (student.Id <= 0)
                throw new ArgumentException("Student ID must be positive", nameof(student));

            ValidateStudent(student);

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = @"
                        UPDATE Students 
                        SET Name = @Name, Age = @Age, Department = @Department, Email = @Email, 
                            PhoneNumber = @PhoneNumber, GPA = @GPA, IsActive = @IsActive, ModifiedDate = @ModifiedDate
                        WHERE Id = @Id";

                    using (var command = new SqlCommand(query, connection))
                    {
                        AddStudentParameters(command, student);
                        command.Parameters.AddWithValue("@Id", student.Id);
                        student.UpdateModifiedDate();

                        connection.Open();
                        var rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            _logger?.LogInformation($"Successfully updated student ID {student.Id}: {student.Name}");
                            return true;
                        }
                        else
                        {
                            _logger?.LogWarning($"No student found with ID {student.Id} for update");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error updating student ID {student.Id}");
                throw new DataException($"Failed to update student with ID {student.Id}", ex);
            }
        }

        /// <summary>
        /// Soft deletes a student (marks as inactive)
        /// </summary>
        /// <param name="id">Student ID to delete</param>
        /// <returns>True if deletion successful, false if student not found</returns>
        public bool SoftDeleteStudent(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Student ID must be positive", nameof(id));

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = "UPDATE Students SET IsActive = 0, ModifiedDate = @ModifiedDate WHERE Id = @Id AND IsActive = 1";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);

                        connection.Open();
                        var rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            _logger?.LogInformation($"Successfully soft deleted student ID {id}");
                            return true;
                        }
                        else
                        {
                            _logger?.LogWarning($"No active student found with ID {id} for deletion");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error soft deleting student ID {id}");
                throw new DataException($"Failed to delete student with ID {id}", ex);
            }
        }

        /// <summary>
        /// Hard deletes a student (permanently removes from database)
        /// </summary>
        /// <param name="id">Student ID to delete</param>
        /// <returns>True if deletion successful, false if student not found</returns>
        public bool DeleteStudent(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Student ID must be positive", nameof(id));

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = "DELETE FROM Students WHERE Id = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        connection.Open();
                        var rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            _logger?.LogInformation($"Successfully deleted student ID {id}");
                            return true;
                        }
                        else
                        {
                            _logger?.LogWarning($"No student found with ID {id} for deletion");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error deleting student ID {id}");
                throw new DataException($"Failed to delete student with ID {id}", ex);
            }
        }

        /// <summary>
        /// Gets student statistics
        /// </summary>
        /// <returns>Dictionary containing various statistics</returns>
        public Dictionary<string, object> GetStudentStatistics()
        {
            var stats = new Dictionary<string, object>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = @"
                        SELECT 
                            COUNT(*) as TotalStudents,
                            COUNT(CASE WHEN IsActive = 1 THEN 1 END) as ActiveStudents,
                            AVG(CAST(Age as FLOAT)) as AverageAge,
                            AVG(CAST(GPA as FLOAT)) as AverageGPA,
                            MAX(GPA) as HighestGPA,
                            MIN(GPA) as LowestGPA,
                            COUNT(DISTINCT Department) as TotalDepartments
                        FROM Students";

                    using (var command = new SqlCommand(query, connection))
                    {
                        connection.Open();
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                stats["TotalStudents"] = reader.GetInt32("TotalStudents");
                                stats["ActiveStudents"] = reader.GetInt32("ActiveStudents");
                                stats["AverageAge"] = reader.IsDBNull("AverageAge") ? 0.0 : reader.GetDouble("AverageAge");
                                stats["AverageGPA"] = reader.IsDBNull("AverageGPA") ? 0.0 : reader.GetDouble("AverageGPA");
                                stats["HighestGPA"] = reader.IsDBNull("HighestGPA") ? 0.0 : reader.GetDecimal("HighestGPA");
                                stats["LowestGPA"] = reader.IsDBNull("LowestGPA") ? 0.0 : reader.GetDecimal("LowestGPA");
                                stats["TotalDepartments"] = reader.GetInt32("TotalDepartments");
                            }
                        }
                    }
                }

                _logger?.LogInformation("Successfully retrieved student statistics");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving student statistics");
                throw new DataException("Failed to retrieve student statistics", ex);
            }

            return stats;
        }

        /// <summary>
        /// Gets department statistics
        /// </summary>
        /// <returns>DataTable containing department statistics</returns>
        public DataTable GetDepartmentStatistics()
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var query = @"
                        SELECT 
                            Department,
                            COUNT(*) as StudentCount,
                            AVG(CAST(Age as FLOAT)) as AverageAge,
                            AVG(CAST(GPA as FLOAT)) as AverageGPA,
                            MAX(GPA) as HighestGPA,
                            MIN(GPA) as LowestGPA
                        FROM Students 
                        WHERE IsActive = 1
                        GROUP BY Department
                        ORDER BY StudentCount DESC";

                    using (var adapter = new SqlDataAdapter(query, connection))
                    {
                        adapter.Fill(dataTable);
                    }
                }

                _logger?.LogInformation($"Retrieved statistics for {dataTable.Rows.Count} departments");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving department statistics");
                throw new DataException("Failed to retrieve department statistics", ex);
            }

            return dataTable;
        }

        /// <summary>
        /// Executes a bulk insert operation for multiple students
        /// </summary>
        /// <param name="students">List of students to insert</param>
        /// <returns>Number of successfully inserted records</returns>
        public int BulkInsertStudents(IEnumerable<Student> students)
        {
            if (students == null)
                throw new ArgumentNullException(nameof(students));

            var studentList = students.ToList();
            if (!studentList.Any())
                return 0;

            var insertedCount = 0;

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var query = @"
                                INSERT INTO Students (Name, Age, Department, Email, PhoneNumber, EnrollmentDate, GPA, IsActive, CreatedDate, ModifiedDate)
                                VALUES (@Name, @Age, @Department, @Email, @PhoneNumber, @EnrollmentDate, @GPA, @IsActive, @CreatedDate, @ModifiedDate)";

                            foreach (var student in studentList)
                            {
                                ValidateStudent(student);

                                using (var command = new SqlCommand(query, connection, transaction))
                                {
                                    AddStudentParameters(command, student);
                                    command.ExecuteNonQuery();
                                    insertedCount++;
                                }
                            }

                            transaction.Commit();
                            _logger?.LogInformation($"Successfully bulk inserted {insertedCount} students");
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error during bulk insert operation. Inserted {insertedCount} out of {studentList.Count} students");
                throw new DataException($"Bulk insert failed after {insertedCount} successful inserts", ex);
            }

            return insertedCount;
        }

        #region Private Helper Methods

        /// <summary>
        /// Maps SqlDataReader to Student object
        /// </summary>
        /// <param name="reader">SqlDataReader instance</param>
        /// <returns>Student object</returns>
        private Student MapReaderToStudent(SqlDataReader reader)
        {
            return new Student
            {
                Id = reader.GetInt32("Id"),
                Name = reader.GetString("Name"),
                Age = reader.GetInt32("Age"),
                Department = reader.GetString("Department"),
                Email = reader.IsDBNull("Email") ? string.Empty : reader.GetString("Email"),
                PhoneNumber = reader.IsDBNull("PhoneNumber") ? string.Empty : reader.GetString("PhoneNumber"),
                EnrollmentDate = reader.IsDBNull("EnrollmentDate") ? DateTime.Now : reader.GetDateTime("EnrollmentDate"),
                GPA = reader.IsDBNull("GPA") ? 0.0m : reader.GetDecimal("GPA"),
                IsActive = reader.IsDBNull("IsActive") || reader.GetBoolean("IsActive"),
                CreatedDate = reader.IsDBNull("CreatedDate") ? DateTime.Now : reader.GetDateTime("CreatedDate"),
                ModifiedDate = reader.IsDBNull("ModifiedDate") ? DateTime.Now : reader.GetDateTime("ModifiedDate")
            };
        }

        /// <summary>
        /// Adds student parameters to SqlCommand
        /// </summary>
        /// <param name="command">SqlCommand instance</param>
        /// <param name="student">Student object</param>
        private void AddStudentParameters(SqlCommand command, Student student)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@Name", student.Name);
            command.Parameters.AddWithValue("@Age", student.Age);
            command.Parameters.AddWithValue("@Department", student.Department);
            command.Parameters.AddWithValue("@Email", string.IsNullOrEmpty(student.Email) ? DBNull.Value : student.Email);
            command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrEmpty(student.PhoneNumber) ? DBNull.Value : student.PhoneNumber);
            command.Parameters.AddWithValue("@EnrollmentDate", student.EnrollmentDate);
            command.Parameters.AddWithValue("@GPA", student.GPA);
            command.Parameters.AddWithValue("@IsActive", student.IsActive);
            command.Parameters.AddWithValue("@CreatedDate", student.CreatedDate);
            command.Parameters.AddWithValue("@ModifiedDate", student.ModifiedDate);
        }

        /// <summary>
        /// Validates student object before database operations
        /// </summary>
        /// <param name="student">Student to validate</param>
        private void ValidateStudent(Student student)
        {
            if (string.IsNullOrWhiteSpace(student.Name))
                throw new ArgumentException("Student name is required");

            if (student.Age < 16 || student.Age > 100)
                throw new ArgumentException("Student age must be between 16 and 100");

            if (string.IsNullOrWhiteSpace(student.Department))
                throw new ArgumentException("Student department is required");

            if (student.GPA < 0.0m || student.GPA > 4.0m)
                throw new ArgumentException("Student GPA must be between 0.0 and 4.0");

            if (!string.IsNullOrEmpty(student.Email) && !IsValidEmail(student.Email))
                throw new ArgumentException("Invalid email format");
        }

        /// <summary>
        /// Validates email format
        /// </summary>
        /// <param name="email">Email to validate</param>
        /// <returns>True if valid, false otherwise</returns>
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

        #region Async Methods

        /// <summary>
        /// Asynchronously gets all students
        /// </summary>
        /// <param name="includeInactive">Whether to include inactive students</param>
        /// <returns>DataTable containing student records</returns>
        public async Task<DataTable> GetAllStudentsAsync(bool includeInactive = true)
        {
            return await Task.Run(() => GetAllStudents(includeInactive));
        }

        /// <summary>
        /// Asynchronously adds a student
        /// </summary>
        /// <param name="student">Student to add</param>
        /// <returns>ID of the newly created student</returns>
        public async Task<int> AddStudentAsync(Student student)
        {
            return await Task.Run(() => AddStudent(student));
        }

        /// <summary>
        /// Asynchronously updates a student
        /// </summary>
        /// <param name="student">Student to update</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> UpdateStudentAsync(Student student)
        {
            return await Task.Run(() => UpdateStudent(student));
        }

        /// <summary>
        /// Asynchronously deletes a student
        /// </summary>
        /// <param name="id">Student ID to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> DeleteStudentAsync(int id)
        {
            return await Task.Run(() => DeleteStudent(id));
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing">Whether disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources here if any
                    _logger?.LogInformation("DatabaseHelper disposed");
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~DatabaseHelper()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// Interface for database operations (useful for testing and dependency injection)
    /// </summary>
    public interface IStudentRepository
    {
        bool TestConnection();
        DataTable GetAllStudents(bool includeInactive = true, int pageNumber = 1, int pageSize = 100);
        Student GetStudentById(int id);
        List<Student> SearchStudents(string searchTerm = null, string department = null, int? minAge = null, int? maxAge = null, decimal? minGpa = null);
        int AddStudent(Student student);
        bool UpdateStudent(Student student);
        bool DeleteStudent(int id);
        bool SoftDeleteStudent(int id);
        Dictionary<string, object> GetStudentStatistics();
        DataTable GetDepartmentStatistics();
        int BulkInsertStudents(IEnumerable<Student> students);
        
        // Async methods
        Task<DataTable> GetAllStudentsAsync(bool includeInactive = true);
        Task<int> AddStudentAsync(Student student);
        Task<bool> UpdateStudentAsync(Student student);
        Task<bool> DeleteStudentAsync(int id);
    }
}