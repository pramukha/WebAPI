using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NLog;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Models;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScandocController : ControllerBase
    {
        private readonly IConfiguration _config;
        Logger logger = LogManager.GetCurrentClassLogger();


        public ScandocController(IConfiguration configuration)
        {
            _config = configuration;
        }
        [HttpPost("register")]
        public IActionResult ClientRegister()
        {
            IActionResult response = Unauthorized();
            Request.Headers.TryGetValue("AccessSecret", out var accessSecret);
            Request.Headers.TryGetValue("AccountId", out var accountId);
            Request.Headers.TryGetValue("ApiKey", out var apiKey);
            var user = new AccountAuthRequest
            {
                AccessSecret = accessSecret,
                AccountId = accountId,
                ApiKey = apiKey
            };

            if (this.AuthenticateUser(user) != null)
            {
                var tokenString = GenerateJSONWebToken(user);
                response = Ok(new AccountAuthResponse { AccessToken = tokenString });
            }

            return response;
        }

        [HttpGet("scan")]
        //[Authorize]
        [EnableCors]
        public async Task<ActionResult> Scan(CancellationToken stoppingToken)
        {
            logger.Info($"Starting Scan Async is cancelled ? :{stoppingToken.IsCancellationRequested} ");
            string retval = "atScan";
            var socket = new ClientWebSocket();
            while (!stoppingToken.IsCancellationRequested)
                //using (var socket = new ClientWebSocket())
                try
                {
                    //logger.Info($" before ConnectAsync");
                    await socket.ConnectAsync(new Uri(_config.GetConnectionString("local")), stoppingToken);
                    //logger.Info($" before send SCAN_DOC");
                    await Send(socket, "SCAN_DOC", stoppingToken);
                    //logger.Info($" before receive");
                    retval = await Receive(socket, stoppingToken);
                    logger.Debug($" retval: {retval}");
                    return Ok(retval);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "WS connection Error.");
                }
            logger.Info($" final retval: {retval}");
            return Ok(retval); ;
        }

        private async Task Send(ClientWebSocket socket, string data, CancellationToken stoppingToken) =>
            await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, stoppingToken);

        private async Task<string> Receive(ClientWebSocket socket, CancellationToken stoppingToken)
        {
            var buffer = new ArraySegment<byte>(new byte[1000000]);
            string returnval = "starting to receive messages";
            logger.Info($" Receive socket call before is cancelled ? :{stoppingToken.IsCancellationRequested}");
            while (!stoppingToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result = null;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        try
                        {
                            //logger.Info($" Receive socket call before ReceiveAsync");
                            result = await socket.ReceiveAsync(buffer, stoppingToken);
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                            //logger.Info($" Receive socket call after ReceiveAsync result:{result.Count}");
                            
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "WS connection Error.");
                            break;
                        }

                    } while (!result.EndOfMessage); // (!result.EndOfMessage)   result.Count<18

                    logger.Info($" after while result:{result.Count}");
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    try
                    {

                        ms.Seek(0, SeekOrigin.Begin);   
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                        {
                            returnval = await reader.ReadToEndAsync();
                        }

                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "WS connection Error.");
                    }
                    return returnval;
                }
            };
            return returnval;
        }
        private string GenerateJSONWebToken(AccountAuthRequest userInfo)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var claims = new[] {
                new Claim("AccessSecret", userInfo.AccessSecret),
                new Claim("AccountId", userInfo.AccountId),
                new Claim("ApiKey", userInfo.ApiKey)
            };

            var token = new JwtSecurityToken(_config["Jwt:Issuer"],
              _config["Jwt:Issuer"],
              claims,
              expires: DateTime.Now.AddYears(1),
              signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private AccountAuthRequest AuthenticateUser(AccountAuthRequest login)
        {
            AccountAuthRequest user = null;

            if (login.AccountId.ToString() == _config["Accout:AccountId"].ToString() && login.AccessSecret.ToString() == _config["Accout:AccessSecret"].ToString() && login.ApiKey.ToString() == _config["Accout:ApiKey"].ToString())
            {
                user = new AccountAuthRequest
                {
                    AccessSecret = login.AccessSecret,
                    AccountId = login.AccountId,
                    ApiKey = login.ApiKey
                };
            }

            return user;
        }

    }
}
