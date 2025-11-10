using Registration.Models;
using System.Threading.Tasks;

namespace Registration.Repository.Interfaces
{
    public interface IReportRepository
    {
        Task<int> SubmitReportAsync(Report report);
    }
}