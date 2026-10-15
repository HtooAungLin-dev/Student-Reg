using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RegistrationApp.Models
{
    public class SubjectItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class StudentFormModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "NRIC is required.")]
        [StringLength(9, MinimumLength = 9, ErrorMessage = "NRIC must be 9 characters.")]
        [RegularExpression(@"^[STFGMstfgm]\d{7}[A-Za-z]$", ErrorMessage = "Invalid NRIC format (e.g. S1234567A).")]
        public string NRIC { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Gender is required.")]
        [RegularExpression("^[MF]$", ErrorMessage = "Gender must be M or F.")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Birthday is required.")]
        [DataType(DataType.Date)]
        public DateTime? Birthday { get; set; }

        [Display(Name = "Available Date")]
        [DataType(DataType.Date)]
        public DateTime? AvailableDate { get; set; }

        public List<int> SelectedSubjectIds { get; set; } = new List<int>();

        public IList<SubjectItem> AllSubjects { get; set; } = new List<SubjectItem>();

        /// <summary>Concurrency token (edit only).</summary>
        public byte[] RowVersion { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Birthday.HasValue)
            {
                if (Birthday.Value.Date > DateTime.Today)
                    yield return new ValidationResult("Birthday cannot be in the future.", new[] { "Birthday" });
                else if (Birthday.Value.Year < 1900)
                    yield return new ValidationResult("Birthday is not valid.", new[] { "Birthday" });
            }
        }
    }

    public class StudentListItem
    {
        public int SN { get; set; }
        public int Id { get; set; }
        public string NRIC { get; set; }
        public string Name { get; set; }
        public string Gender { get; set; }
        public int Age { get; set; }
        public int SubjectCount { get; set; }
    }

    public class StudentListViewModel
    {
        public string Query { get; set; }
        public IList<StudentListItem> Students { get; set; } = new List<StudentListItem>();
    }
}
