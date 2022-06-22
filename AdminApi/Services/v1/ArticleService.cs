using AdminShared.Models;
using Common;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminApi.Services.v1
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class ArticleService
    {

        private readonly DatabaseContext db;

        public ArticleService(DatabaseContext db)
        {
            this.db = db;
        }



        public List<DtoKeyValueChild>? GetCategoryChildList(long categoryId)
        {
            var list = db.TCategory.Where(t => t.IsDelete == false && t.ParentId == categoryId).Select(t => new DtoKeyValueChild
            {
                Key = t.Id,
                Value = t.Name,
            }).ToList();

            foreach (var item in list)
            {
                item.ChildList = GetCategoryChildList(Convert.ToInt64(item.Key));
            }

            return list;
        }

    }
}
