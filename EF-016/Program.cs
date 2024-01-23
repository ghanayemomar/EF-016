using C01.SplitQuery.QueryData.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace C01.SplitQueries
{
    class Program
    {
        public static void Main(string[] args)
        {
            Pagination();

        }
        public static void ProperProjection()
        {
            using (var context = new AppDbContext())
            {
                // proper projection (select) reduce network traffic and reduce the effect on app performance
                var coursesProjection = context.Courses.AsNoTracking()
                    .Select(c =>
                    new
                    {
                        CourseId = c.Id,
                        CourseName = c.CourseName,
                        Hours = c.HoursToComplete,
                        Section = c.Sections.Select(s =>
                        new
                        {
                            SectionId = s.Id,
                            SectionName = s.SectionName,
                            DateRate = s.DateRange.ToString(),
                            TimeSlot = s.TimeSlot.ToString()
                        }),
                        Reviews = c.Reviews.Select(r =>
                        new
                        {
                            FeedBack = r.Feedback,
                            CreateAt = r.CreatedAt
                        })
                    }).ToList();
            }
        }
        private static void SplittingQuery()
        {
            using (var context = new AppDbContext())
            {
                //var courses1 = context.Courses
                //    .Include(x => x.Sections)
                //    .Include(x => x.Reviews)
                //    .AsSplitQuery() // explicit
                //    .ToList();

                //var courses2 = context.Courses
                //  .Include(x => x.Sections)
                //  .Include(x => x.Reviews) // split by config
                //  .ToList();

                var courses3 = context.Courses
                .Include(x => x.Sections)
                .Include(x => x.Reviews) // split by config
                .AsSingleQuery()
                .ToList();
            }
        }
        public static void InnerJoin()
        {
            using (var context = new AppDbContext())
            {
                var resultMethodSyntax = context.Courses.AsNoTracking().Join(context.Sections.AsNoTracking(), c => c.Id, s => s.CourseId, (c, s) => new
                {
                    c.CourseName,
                    DateRange = s.DateRange.ToString(),
                    TimeSlot = s.TimeSlot.ToString(),
                }).ToList();
            }
        }
        public static void LeftJoin()
        {
            using (var context = new AppDbContext())
            {
                var officeOccupancyMethodSyntax = context.Offices
                 .GroupJoin(
                     context.Instructors,
                     o => o.Id,
                     i => i.OfficeId,
                     (office, instructor) => new { office, instructor }
                 )
                 .SelectMany(
                     ov => ov.instructor.DefaultIfEmpty(),
                     (ov, instructor) => new
                     {
                         OfficeId = ov.office.Id,
                         Name = ov.office.OfficeName,
                         Location = ov.office.OfficeLocation,
                         Instructor = instructor != null ? instructor.FName : "<<EMPTY>>"
                     }
                 ).ToList();

                foreach (var office in officeOccupancyMethodSyntax)
                {
                    Console.WriteLine($"{office.Name} -> {office.Instructor}");
                }
            }
        }
        public static void CrossJoin()
        {
            using (var context = new AppDbContext())
            {
                var sectionInstructorMethodSyntax = context.Sections
                    .SelectMany(
                        s => context.Instructors,
                        (s, i) => new { s.SectionName, i.FName }
                    )
                    .ToList();

                Console.WriteLine(sectionInstructorMethodSyntax.Count);
            }

        }
        public static void SelectMany()
        {
            using (var context = new AppDbContext())
            {
                var frontEndParticipants = context.Courses.Where(x => x.CourseName.Contains("frontend")).SelectMany(x => x.Sections).SelectMany(x => x.Participants).Select(p => new
                {
                    ParticipantName = p.FullName
                });
                foreach (var pName in frontEndParticipants)
                    Console.WriteLine(pName);
            }

        }
        public static void GroupBy()
        {
            using (var context = new AppDbContext())
            {
                var instructorSection = context.Sections.GroupBy(x => x.Instructor).Select(x => new
                {
                    Key = x.Key,
                    TotalSections = x.Count()
                });
                foreach (var item in instructorSection)
                {
                    Console.WriteLine($"{item.Key.FName}" + $"==> Total Sections #[{item.TotalSections}]");
                }
            }



        }

        public static void Pagination()
        {
            using (var context = new AppDbContext())
            {
                var pageNumber = 1;
                var pageSize = 10;
                var totalSections = context.Sections.Count();
                var totalPages = (int)Math.Ceiling((double)totalSections / pageSize);

                var query = context.Sections.AsNoTracking().Include(x => x.Course).Include(x => x.Instructor).Include(x => x.Schedule).Select(x => new
                {
                    Course = x.Course.CourseName,
                    Instructor = x.Instructor.FName,
                    DateRange = x.DateRange.ToString(),
                    TimeSlot = x.TimeSlot.ToString(),
                    Days = string.Join(" ",   // "SAT SUN MON"
                               x.Schedule.SUN ? "SUN" : "",
                               x.Schedule.SAT ? "SAT" : "",
                               x.Schedule.MON ? "MON" : "",
                               x.Schedule.TUE ? "TUE" : "",
                               x.Schedule.WED ? "WED" : "",
                               x.Schedule.THU ? "THU" : "",
                               x.Schedule.FRI ? "FRI" : "")
                });


                Console.WriteLine("|           Course                   |          Instructor            |       Date Range        |   Time Slot   |            Days                |");
                Console.WriteLine("|------------------------------------|--------------------------------|-------------------------|---------------|--------------------------------|");


                while (pageNumber <= totalPages)
                {
                    // actual paging
                    var pageResult = query.Skip(pageNumber - 1).Take(pageSize);


                    foreach (var section in pageResult)
                    {
                        Console.WriteLine($"| {section.Course,-34} | {section.Instructor,-30} | {section.DateRange.ToString(),-23} | {section.TimeSlot.ToString(),-12} | {section.Days,-30} |");
                    }

                    Console.WriteLine();

                    for (int p = 1; p <= totalPages; p++)
                    {
                        Console.ForegroundColor = p == pageNumber ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
                        Console.Write($"{p} "); // 1 2 3 4 5 .... 20
                    }
                    Console.ForegroundColor = ConsoleColor.White;
                    ++pageNumber;

                    Console.ReadKey();
                    Console.Clear();
                }
                Console.ReadKey();
            }
        }
    }
}