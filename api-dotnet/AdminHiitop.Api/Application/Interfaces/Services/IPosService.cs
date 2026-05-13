using AdminHiitop.Api.Application.DTOs.Pos;

namespace AdminHiitop.Api.Application.Interfaces.Services;

public interface IPosService
{
    Task<PosInitialDataResponse> GetInitialDataAsync();
}
