using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly LinkGenerator _linkGenerator;

    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _linkGenerator = linkGenerator;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [HttpHead("{userId}")]
    [Produces("application/json", "application/xml")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = _userRepository.FindById(userId);
        if (user is null) return NotFound();
        if (HttpMethods.IsHead(Request.Method))
        {
           Response.Headers.Append("Content-Type", "application/json; charset=utf-8");
            return Ok();
        }
        return Ok(_mapper.Map<UserDto>(user));
    }

    [HttpPost]
    [Produces("application/json", "application/xml")]
    public IActionResult CreateUser([FromBody] UserForCreationDto? user)
    {
        if (user is null) return BadRequest();

        var createdUserEntity = _userRepository.Insert(_mapper.Map<UserEntity>(user));

        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);
        
        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = createdUserEntity.Id },
            createdUserEntity.Id);
    }

    [HttpPut("{userId}")]
    [Produces("application/json", "application/xml")] 
    public IActionResult UpsertUser([FromRoute] Guid userId, [FromBody] UserForUpdatingDto? user)
    {
        if (user is null || userId == Guid.Empty) return BadRequest();
        if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

        _userRepository.UpdateOrInsert(_mapper.Map(user, new UserEntity(userId)), out var created);
        
        return created ? CreatedAtRoute(
            nameof(GetUserById), 
            new {userId}, 
            userId) : NoContent();
    }

    [HttpPatch("{userId}")]
    [Produces("application/json", "application/xml")]
    public IActionResult PartiallyUpdateUser([FromRoute] Guid userId, [FromBody] JsonPatchDocument<UserForUpdatingDto>? patchDoc)
    {
        if (patchDoc is null) return BadRequest();
        var user = _userRepository.FindById(userId);
        if (user is null) return NotFound();
        var updateDto = new UserForUpdatingDto();
        patchDoc.ApplyTo(updateDto, ModelState);
        if (!TryValidateModel(updateDto)) return UnprocessableEntity(ModelState);
        _userRepository.Update(_mapper.Map(updateDto, new UserEntity(userId)));
        return NoContent();
    }

    [HttpDelete("{userId}")]
    [Produces("application/json", "application/xml")]
    public IActionResult DeleteUser([FromRoute] Guid userId)
    {
        var user = _userRepository.FindById(userId);
        if (user is null) return NotFound();
        _userRepository.Delete(userId);
        return NoContent();
    }

    [HttpGet(Name = nameof(GetUsers))]
    [Produces("application/json", "application/xml")]
    public IActionResult GetUsers(
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 10)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Max(1, Math.Min(20, pageSize));
        var pageList = _userRepository.GetPage(pageNumber , pageSize);
        var users = _mapper.Map<IEnumerable<UserDto>>(pageList);
        var paginationHeader = new
        {
            previousPageLink = pageNumber - 1 == 0 ? null :  
                _linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), new { pageSize, pageNumber = pageNumber - 1}),
            nextPageLink = _linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), new { pageSize, pageNumber = pageNumber + 1}),
            totalCount = users.Count(),
            pageSize,
            currentPage = pageNumber,
            totalPages = pageSize
        };
        Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
        return Ok(users);
    }

    [HttpOptions]
    [Produces("application/json", "application/xml")]
    public IActionResult GetUsersOptions()
    {
        Response.Headers.Append("Allow", "GET, POST, OPTIONS");
        return Ok();
    }
}