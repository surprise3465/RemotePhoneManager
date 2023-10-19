using Microsoft.AspNetCore.Mvc;
using RemotePhone.Database;
using RemotePhone.Models;
using RemotePhone.Services;

namespace RemotePhone.Controllers
{
    ///<summary>
    ///PhoneController
    ///</summary>
    [ApiController]
    [Route("[controller]")]
    public class PhoneController : ControllerBase
    {
        private readonly ILoggerService _logger;
        private readonly IStartUpService _service;

        public PhoneController(IStartUpService service,
            ILoggerService logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("Port")]
        public async Task<ActionResult<PortStatusResponse>> GetPort(string type)
        {
            try
            {
                PortStatusResponse status = new PortStatusResponse();

                if (!(type == "real" ||  type == "virtual"))
                {
                    status.Status = "Fail";
                    status.Message = "Wrong Type";
                    return BadRequest(status);
                }

                if (type == "real")
                {
                    var phone = await _service.GetRealPhoneSerial();
                    if (phone != null)
                    {
                        status.UserId = phone.UserId;
                        status.Status = "Success";
                        status.Message = phone.Serial;
                    }
                    else
                    {
                        status.Status = "Fail";
                        status.Message = "No available phone";
                    }
                }
                else if (type == "virtual")
                {
                    var vphone = await _service.GetVirtualPhonePort();
                    if (vphone != null)
                    {
                        status.UserId = vphone.UserId;
                        status.Status = "Success";
                        status.Message = vphone.Port.ToString();
                    }
                    else
                    {
                        status.Status = "Fail";
                        status.Message = "no available virtual phone";
                    }
                }

                return Ok(status);
            }
            catch (Exception e)
            {
                return StatusCode(502, $"{e.Message} - {e.InnerException}");
            }
        }

        [HttpPost("HeartBeat")]
        public async Task<ActionResult<StatusResponse>> PostHeartBeat([FromBody] HeartBeat heart)
        {
            StatusResponse status = new StatusResponse()
            {
                Status = "Success",
            };

            if(heart.UserId == null)
            {
                status.Status = "Fail";
                status.Message = "UserId is null";
                return BadRequest(status);
            }

            var phone = await _service.UpdateRealVisit(heart.UserId);
            if (phone != null)
            {
                status.Message += $"更新{phone.Serial}访问时间";
            }

            var vphone = await _service.UpdateVirtualVisit(heart.UserId);
            if (vphone != null)
            {
                status.Message += $" 更新{vphone.Port}访问时间";
            }

            if (phone == null && vphone == null)
            {
                status.Status = "Fail";
                status.Message = "没有正在使用的Phone";
            }

            return Ok(status);
        }

        [HttpPost("Exit")]
        public async Task<ActionResult<StatusResponse>> Exit([FromBody] HeartBeat heart)
        {
            StatusResponse status = new StatusResponse()
            {
                Status = "Success",
            };

            if (heart.UserId == null)
            {
                status.Status = "Fail";
                status.Message = "UserId is null";
                return BadRequest(status);
            }

            var phone = await _service.ExitRealPhone(heart.UserId);
            if (phone != null)
            {
                status.Message += $"释放{phone.Serial}";
            }

            var vphone = await _service.ExitVirtualPhone(heart.UserId);
            if (vphone != null)
            {
                status.Message += $" 释放{vphone.Port}";
            }

            if (phone == null && vphone == null)
            {
                status.Status = "Fail";
                status.Message = "没有正在使用的Phone";
            }

            return Ok(status);
        }
    }
}


