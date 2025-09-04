using System;
using System.ComponentModel.DataAnnotations;

namespace StudentManager
{
    /// <summary>
    /// Represents a student entity with validation attributes and comprehensive properties
    /// </summary>
    public class Student : IEquatable<Student>, ICloneable
    {
        /// <summary>
        /// Gets or sets the unique identifier for the student
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the student's full name
        /// </summary>
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the student's age
        /// </summary>
        [Range(16, 100, ErrorMessage = "Age must be between 16 and 100")]
        public int Age { get; set; }

        /// <summary>
        /// Gets or sets the student's department
        /// </summary>
        [Required(ErrorMessage = "Department is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Department must be between 2 and 50 characters")]
        public string Department { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the student's email address
        /// </summary>
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the student's phone number
        /// </summary>
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(15, ErrorMessage = "Phone number cannot exceed 15 characters")]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the student's enrollment date
        /// </summary>
        public DateTime EnrollmentDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the student's GPA
        /// </summary>
        [Range(0.0, 4.0, ErrorMessage = "GPA must be between 0.0 and 4.0")]
        public decimal GPA { get; set; } = 0.0m;

        /// <summary>
        /// Gets or sets whether the student is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the date when the record was created
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the date when the record was last modified
        /// </summary>
        public DateTime ModifiedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Default constructor
        /// </summary>
        public Student()
        {
        }

        /// <summary>
        /// Constructor with basic parameters
        /// </summary>
        /// <param name="name">Student's name</param>
        /// <param name="age">Student's age</param>
        /// <param name="department">Student's department</param>
        public Student(string name, int age, string department)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Age = age;
            Department = department ?? throw new ArgumentNullException(nameof(department));
        }

        /// <summary>
        /// Full constructor with all parameters
        /// </summary>
        public Student(int id, string name, int age, string department, string email, 
                      string phoneNumber, decimal gpa, bool isActive = true)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Age = age;
            Department = department ?? throw new ArgumentNullException(nameof(department));
            Email = email ?? string.Empty;
            PhoneNumber = phoneNumber ?? string.Empty;
            GPA = gpa;
            IsActive = isActive;
        }

        /// <summary>
        /// Validates the student object and returns validation results
        /// </summary>
        /// <returns>ValidationResult collection</returns>
        public System.Collections.Generic.IEnumerable<ValidationResult> Validate()
        {
            var results = new System.Collections.Generic.List<ValidationResult>();
            var context = new ValidationContext(this);
            Validator.TryValidateObject(this, context, results, true);
            return results;
        }

        /// <summary>
        /// Checks if the student object is valid
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            var results = Validate();
            return !results.Any();
        }

        /// <summary>
        /// Returns a string representation of the student
        /// </summary>
        /// <returns>Formatted string with student information</returns>
        public override string ToString()
        {
            return $"Student [Id: {Id}, Name: {Name}, Age: {Age}, Department: {Department}, GPA: {GPA:F2}]";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current student
        /// </summary>
        /// <param name="obj">The object to compare</param>
        /// <returns>True if equal, false otherwise</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as Student);
        }

        /// <summary>
        /// Determines whether the specified student is equal to the current student
        /// </summary>
        /// <param name="other">The student to compare</param>
        /// <returns>True if equal, false otherwise</returns>
        public bool Equals(Student other)
        {
            return other != null &&
                   Id == other.Id &&
                   Name == other.Name &&
                   Age == other.Age &&
                   Department == other.Department;
        }

        /// <summary>
        /// Returns a hash code for the current student
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name, Age, Department);
        }

        /// <summary>
        /// Creates a deep copy of the current student
        /// </summary>
        /// <returns>Cloned student object</returns>
        public object Clone()
        {
            return new Student(Id, Name, Age, Department, Email, PhoneNumber, GPA, IsActive)
            {
                EnrollmentDate = this.EnrollmentDate,
                CreatedDate = this.CreatedDate,
                ModifiedDate = this.ModifiedDate
            };
        }

        /// <summary>
        /// Updates the modified date to current time
        /// </summary>
        public void UpdateModifiedDate()
        {
            ModifiedDate = DateTime.Now;
        }

        /// <summary>
        /// Gets the age group category for the student
        /// </summary>
        /// <returns>Age group as string</returns>
        public string GetAgeGroup()
        {
            return Age switch
            {
                < 18 => "Minor",
                >= 18 and < 25 => "Young Adult",
                >= 25 and < 35 => "Adult",
                _ => "Mature Adult"
            };
        }

        /// <summary>
        /// Gets the GPA letter grade
        /// </summary>
        /// <returns>Letter grade as string</returns>
        public string GetLetterGrade()
        {
            return GPA switch
            {
                >= 3.7m => "A",
                >= 3.3m => "A-",
                >= 3.0m => "B+",
                >= 2.7m => "B",
                >= 2.3m => "B-",
                >= 2.0m => "C+",
                >= 1.7m => "C",
                >= 1.3m => "C-",
                >= 1.0m => "D",
                _ => "F"
            };
        }

        // Operator overloads
        public static bool operator ==(Student left, Student right)
        {
            return EqualityComparer<Student>.Default.Equals(left, right);
        }

        public static bool operator !=(Student left, Student right)
        {
            return !(left == right);
        }
    }

    /// <summary>
    /// Extension methods for Student class
    /// </summary>
    public static class StudentExtensions
    {
        /// <summary>
        /// Formats student name for display
        /// </summary>
        /// <param name="student">Student instance</param>
        /// <returns>Formatted name</returns>
        public static string GetFormattedName(this Student student)
        {
            if (string.IsNullOrWhiteSpace(student.Name))
                return "Unknown";

            return student.Name.Trim().ToTitleCase();
        }

        /// <summary>
        /// Converts string to title case
        /// </summary>
        /// <param name="input">Input string</param>
        /// <returns>Title case string</returns>
        private static string ToTitleCase(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
        }
    }
}