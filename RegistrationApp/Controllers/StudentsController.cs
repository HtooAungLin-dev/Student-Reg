using System.Linq;
using System.Web.Mvc;
using RegistrationApp.Data;
using RegistrationApp.Models;

namespace RegistrationApp.Controllers
{
    public class StudentsController : Controller
    {
        private readonly StudentRepository _repo = new StudentRepository();

        // ===================== Part 1: Registration =====================

        [HttpGet]
        public ActionResult Register()
        {
            AppLogger.Action("Register.Open");
            return View(new StudentFormModel { AllSubjects = _repo.GetSubjects() });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Register(StudentFormModel model)
        {
            model.AllSubjects = _repo.GetSubjects();
            Normalize(model);

            if (ModelState.IsValid)
            {
                try
                {
                    int id = _repo.Create(model);
                    AppLogger.Action("Student.Register", "Id=" + id + "; NRIC=" + model.NRIC);
                    TempData["Message"] = "Student " + model.NRIC + " registered successfully.";
                    return RedirectToAction("Index");
                }
                catch (DuplicateNricException ex)
                {
                    ModelState.AddModelError("NRIC", ex.Message);
                    AppLogger.Action("Student.Register.Rejected", "Duplicate NRIC=" + model.NRIC);
                }
            }
            else
            {
                AppLogger.Action("Student.Register.Invalid", "NRIC=" + model.NRIC);
            }
            return View(model);
        }

        // ===================== Part 2: View all / search =====================

        [HttpGet]
        public ActionResult Index(string q)
        {
            var students = _repo.Search(q);
            AppLogger.Action(string.IsNullOrWhiteSpace(q) ? "Student.List" : "Student.Search",
                             "q=" + q + "; results=" + students.Count);
            return View(new StudentListViewModel { Query = q, Students = students });
        }

        // ===================== Edit (opened from the NRIC link) =====================

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var model = _repo.GetById(id);
            if (model == null) return HttpNotFound();

            model.AllSubjects = _repo.GetSubjects();
            AppLogger.Action("Student.Edit.Open", "Id=" + id);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(StudentFormModel model)
        {
            model.AllSubjects = _repo.GetSubjects();
            Normalize(model);

            if (ModelState.IsValid)
            {
                try
                {
                    if (_repo.Update(model))
                    {
                        AppLogger.Action("Student.Update", "Id=" + model.Id + "; NRIC=" + model.NRIC);
                        TempData["Message"] = "Student " + model.NRIC + " updated successfully.";
                        return RedirectToAction("Index");
                    }
                    ModelState.AddModelError("", "This record was changed or deleted by someone else. Please go back to the list and try again.");
                    AppLogger.Action("Student.Update.Conflict", "Id=" + model.Id);
                }
                catch (DuplicateNricException ex)
                {
                    ModelState.AddModelError("NRIC", ex.Message);
                    AppLogger.Action("Student.Update.Rejected", "Duplicate NRIC=" + model.NRIC);
                }
            }
            return View(model);
        }

        // ---------------------------------------------------------------- helpers
        /// <summary>Normalise input and drop any tampered subject ids (server never trusts the client).</summary>
        private static void Normalize(StudentFormModel m)
        {
            m.NRIC = (m.NRIC ?? "").Trim().ToUpperInvariant();
            m.Name = (m.Name ?? "").Trim();
            var valid = m.AllSubjects.Select(s => s.Id).ToList();
            m.SelectedSubjectIds = (m.SelectedSubjectIds ?? new System.Collections.Generic.List<int>())
                                   .Where(valid.Contains).Distinct().ToList();
        }
    }
}
