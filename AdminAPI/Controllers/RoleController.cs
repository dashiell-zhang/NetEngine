using AdminAPI.Filters;
using AdminAPI.Libraries;
using AdminAPI.Services;
using AdminShared.Models;
using AdminShared.Models.Role;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;

namespace AdminAPI.Controllers
{


    [SignVerifyFilter]
    [Route("[controller]")]
    [Authorize]
    [ApiController]
    public class RoleController : ControllerBase
    {


        private readonly DatabaseContext db;
        private readonly IDHelper idHelper;
        private readonly RoleService roleService;

        private readonly long userId;


        public RoleController(DatabaseContext db, IDHelper idHelper, IHttpContextAccessor httpContextAccessor, RoleService roleService)
        {
            this.db = db;
            this.idHelper = idHelper;
            this.roleService = roleService;

            var userIdStr = httpContextAccessor.HttpContext?.GetClaimByAuthorization("userId");

            if (userIdStr != null)
            {
                userId = long.Parse(userIdStr);
            }
        }



        /// <summary>
        /// 获取角色列表
        /// </summary>
        /// <param name="pageNum">页码</param>
        /// <param name="pageSize">单页数量</param>
        /// <param name="searchKey">搜索关键字</param>
        /// <returns></returns>
        [HttpGet("GetRoleList")]
        public DtoPageList<DtoRole> GetRoleList(int pageNum, int pageSize, string? searchKey)
        {

            var retList = new DtoPageList<DtoRole>();

            int skip = (pageNum - 1) * pageSize;

            var query = db.TRole.Where(t => t.IsDelete == false);


            if (!string.IsNullOrEmpty(searchKey))
            {
                query = query.Where(t => t.Name.Contains(searchKey));
            }

            retList.Total = query.Count();

            retList.List = query.Select(t => new DtoRole
            {
                Id = t.Id,
                CreateTime = t.CreateTime,
                Name = t.Name,
                Remarks = t.Remarks
            }).Skip(skip).Take(pageSize).ToList();

            return retList;
        }



        /// <summary>
        /// 通过ID获取角色信息
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        [HttpGet("GetRole")]
        public DtoRole? GetRole(long roleId)
        {

            var role = db.TRole.Where(t => t.IsDelete == false && t.Id == roleId).Select(t => new DtoRole
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
        [HttpPost("CreateRole")]
        public long CreateRole(DtoEditRole role)
        {
            var dbRole = new TRole
            {
                Id = idHelper.GetId(),
                CreateTime = DateTime.UtcNow,

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
        [HttpPost("UpdateRole")]
        public bool UpdateRole(long roleId, DtoEditRole role)
        {
            var dbRole = db.TRole.Where(t => t.IsDelete == false && t.Id == roleId).FirstOrDefault();

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
        [HttpDelete("DeleteRole")]
        public bool DeleteRole(long id)
        {

            var role = db.TRole.Where(t => t.IsDelete == false && t.Id == id).FirstOrDefault();

            var isHaveUser = db.TUserRole.Where(t => t.IsDelete == false && t.RoleId == id).FirstOrDefault();

            if (isHaveUser != null)
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "当前角色下存在人员信息，无法删除！");

                return false;
            }

            if (role != null)
            {
                role.IsDelete = true;
                role.DeleteTime = DateTime.UtcNow;

                db.SaveChanges();

                return true;
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "角色不存在或已被删除！");

                return false;
            }
        }




        /// <summary>
        /// 获取某个角色的功能权限
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        [HttpGet("GetRoleFunction")]
        public List<DtoRoleFunction> GetRoleFunction(long roleId)
        {
            var functionList = db.TFunction.Where(t => t.IsDelete == false && t.ParentId == null).Select(t => new DtoRoleFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Type = t.Type.ToString(),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.IsDelete == false && r.FunctionId == t.Id && r.RoleId == roleId).FirstOrDefault() != null,
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
        [HttpPost("SetRoleFunction")]
        public bool SetRoleFunction(DtoSetRoleFunction setRoleFunction)
        {

            var functionAuthorize = db.TFunctionAuthorize.Where(t => t.IsDelete == false && t.RoleId == setRoleFunction.RoleId && t.FunctionId == setRoleFunction.FunctionId).FirstOrDefault() ?? new TFunctionAuthorize();

            functionAuthorize.FunctionId = setRoleFunction.FunctionId;
            functionAuthorize.RoleId = setRoleFunction.RoleId;

            if (setRoleFunction.IsCheck)
            {
                if (functionAuthorize.Id == default)
                {
                    functionAuthorize.Id = idHelper.GetId();
                    functionAuthorize.CreateTime = DateTime.UtcNow;
                    functionAuthorize.CreateUserId = userId;

                    db.TFunctionAuthorize.Add(functionAuthorize);
                }
            }
            else
            {
                if (functionAuthorize.Id != default)
                {
                    functionAuthorize.IsDelete = true;
                    functionAuthorize.DeleteTime = DateTime.UtcNow;
                    functionAuthorize.DeleteUserId = userId;
                }
            }

            db.SaveChanges();

            return true;
        }


    }
}