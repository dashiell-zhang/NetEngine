using AdminAPI.Services;
using AdminShared.Models;
using AdminShared.Models.Role;
using Common;
using IdentifierGenerator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using WebAPIBasic.Filters;
using WebAPIBasic.Libraries;

namespace AdminAPI.Controllers
{


    [SignVerifyFilter]
    [Route("[controller]/[action]")]
    [Authorize]
    [ApiController]
    public class RoleController : ControllerBase
    {


        private readonly DatabaseContext db;
        private readonly IdService idService;
        private readonly RoleService roleService;

        private readonly long userId;


        public RoleController(DatabaseContext db, IdService idService, IHttpContextAccessor httpContextAccessor, RoleService roleService)
        {
            this.db = db;
            this.idService = idService;
            this.roleService = roleService;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByUser("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }



        /// <summary>
        /// 获取角色列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public DtoPageList<DtoRole> GetRoleList([FromQuery] DtoPageRequest request)
        {
            var retList = new DtoPageList<DtoRole>();

            var query = db.TRole.AsQueryable();

            retList.Total = query.Count();

            retList.List = query.OrderBy(t => t.CreateTime).Select(t => new DtoRole
            {
                Id = t.Id,
                CreateTime = t.CreateTime,
                Name = t.Name,
                Remarks = t.Remarks
            }).Skip(request.Skip()).Take(request.PageSize).ToList();

            return retList;
        }



        /// <summary>
        /// 通过ID获取角色信息
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        [HttpGet]
        public DtoRole? GetRole(long roleId)
        {

            var role = db.TRole.Where(t => t.Id == roleId).Select(t => new DtoRole
            {
                Id = t.Id,
                CreateTime = t.CreateTime,
                Name = t.Name,
                Remarks = t.Remarks
            }).FirstOrDefault();

            return role;
        }



        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        [QueueLimitFilter(IsBlock = true, IsUseToken = true)]
        [HttpPost]
        public long CreateRole(DtoEditRole role)
        {
            var dbRole = new TRole
            {
                Id = idService.GetId(),
                Name = role.Name,
                Remarks = role.Remarks
            };

            db.TRole.Add(dbRole);

            db.SaveChanges();

            return dbRole.Id;
        }



        /// <summary>
        /// 编辑角色
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        [QueueLimitFilter(IsBlock = true, IsUseToken = true)]
        [HttpPost]
        public bool UpdateRole(long roleId, DtoEditRole role)
        {
            var dbRole = db.TRole.Where(t => t.Id == roleId).FirstOrDefault();

            if (dbRole != null)
            {
                dbRole.Name = role.Name;
                dbRole.Remarks = role.Remarks;

                db.SaveChanges();

                return true;
            }
            else
            {
                return false;
            }
        }




        /// <summary>
        /// 删除角色
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        public bool DeleteRole(long id)
        {

            var role = db.TRole.Where(t => t.Id == id).FirstOrDefault();

            var isHaveUser = db.TUserRole.Where(t => t.RoleId == id).FirstOrDefault();

            if (isHaveUser != null)
            {
                throw new CustomException("当前角色下存在人员信息，无法删除！");
            }

            if (role != null)
            {
                role.IsDelete = true;

                db.SaveChanges();

                return true;
            }
            else
            {
                throw new CustomException("角色不存在或已被删除！");
            }
        }




        /// <summary>
        /// 获取某个角色的功能权限
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        [HttpGet]
        public List<DtoRoleFunction> GetRoleFunction(long roleId)
        {
            var functionList = db.TFunction.Where(t => t.ParentId == null && t.Type == TFunction.EnumType.模块).Select(t => new DtoRoleFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == t.Id && r.RoleId == roleId).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f => f.ParentId == t.Id && f.Type == TFunction.EnumType.功能).Select(f => new DtoRoleFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == f.Id && r.RoleId == roleId).FirstOrDefault() != null,
                }).ToList()
            }).ToList();

            foreach (var function in functionList)
            {
                function.ChildList = roleService.GetRoleFunctionChildList(roleId, function.Id);
            }

            return functionList;
        }




        /// <summary>
        /// 设置角色的功能
        /// </summary>
        /// <param name="setRoleFunction"></param>
        /// <returns></returns>
        [HttpPost]
        public bool SetRoleFunction(DtoSetRoleFunction setRoleFunction)
        {

            var functionAuthorize = db.TFunctionAuthorize.Where(t => t.RoleId == setRoleFunction.RoleId && t.FunctionId == setRoleFunction.FunctionId).FirstOrDefault() ?? new TFunctionAuthorize();

            functionAuthorize.FunctionId = setRoleFunction.FunctionId;
            functionAuthorize.RoleId = setRoleFunction.RoleId;

            if (setRoleFunction.IsCheck)
            {
                if (functionAuthorize.Id == default)
                {
                    functionAuthorize.Id = idService.GetId();
                    functionAuthorize.CreateUserId = userId;

                    db.TFunctionAuthorize.Add(functionAuthorize);
                }
            }
            else
            {
                if (functionAuthorize.Id != default)
                {
                    functionAuthorize.IsDelete = true;
                    functionAuthorize.DeleteUserId = userId;
                }
            }

            db.SaveChanges();

            return true;
        }



        /// <summary>
        /// 获取角色键值对
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public List<DtoKeyValue> GetRoleKV()
        {
            var list = db.TRole.Select(t => new DtoKeyValue
            {
                Key = t.Id,
                Value = t.Name
            }).ToList();

            return list;
        }
    }
}