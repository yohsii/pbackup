﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using puck.core.Base;
using puck.core.Entities;
using puck.core.Models;

namespace puck.core.Abstract
{
    public interface I_Content_Service
    {
        IConfiguration config { get; set; }
        I_Content_Indexer indexer { get; set; }
        I_Log logger { get; set; }
        I_Puck_Repository repo { get; set; }
        RoleManager<PuckRole> roleManager { get; set; }
        I_Task_Dispatcher tdispatcher { get; set; }
        UserManager<PuckUser> userManager { get; set; }

        void AddAuditEntry(Guid id, string variant, string action, string notes, string username,bool save=true,DateTime? timestamp=null);
        void AddPublishInstruction(List<BaseModel> toIndex,bool save=true);
        Task Sync(Guid id, Guid parentId, bool includeDescendants, bool onlyOverwriteIfNewer, I_Content_Service destinationContentService, IMemoryCache cache, string cacheKey, string userName = null);
        Task Copy(Guid id, Guid parentId, bool includeDescendants, string userName = null);
        Task<T> Create<T>(Guid parentId, string variant, string name, string template = null, bool published = true, string userName = null) where T : BaseModel;
        Task Delete(Guid id, string variant = null, string userName = null);
        int DeleteRevisions(List<Guid> ids, int step = 100);
        string GetIdPath(BaseModel mod);
        string GetLiveOrCurrentPath(Guid id);
        Task Move(Guid nodeId, Guid destinationId, string userName = null);
        Task Move(string start, string destination);
        Task Publish(Guid id, string variant, List<string> descendantVariants, string userName = null);
        Task UnPublish(Guid id, string variant, List<string> descendantVariants, string userName = null);
        Task RePublish(Guid id, string variant, List<string> descendantVariants, string userName = null);
        Task Publish(Guid id, List<string> variants, List<string> descendantVariants, string userName = null);
        Task UnPublish(Guid id, List<string> variants, List<string> descendantVariants, string userName = null);
        Task RePublish(Guid id, List<string> variants, List<string> descendantVariants, string userName = null);
        void RenameOrphaned(string orphanTypeName, string newTypeName);
        int RenameOrphaned2(string orphanTypeName, string newTypeName);
        Task RePublishEntireSite();
        Task RePublishEntireSite2(bool addInstruction = false);
        Task<SaveResult> SaveContent<T>(T mod, bool makeRevision = true, string userName = null, bool handleNodeNameExists = true, int nodeNameExistsCounter = 0, bool triggerEvents = true, bool triggerIndexEvents = true, bool shouldIndex = true,bool alwaysUpdatePath=true, bool queueIfIndexerBusy = false) where T : BaseModel;
        void Sort(Guid parentId, List<Guid> ids);
        int UpdateDescendantHasNoPublishedRevision(string path, bool value, List<string> descendantVariants);
        int UpdateDescendantIdPaths(string oldPath, string newPath);
        int UpdateDescendantIsPublishedRevision(string path, bool value, bool addWhereIsCurrentClause, List<string> descendantVariants);
        int UpdateDescendantPaths(string oldPath, string newPath);
        void UpdatePathRelatedMeta(string oldPath, string newPath,bool save = true);
        int UpdateTypeAndTypeChain(string oldType, string newType, string newTypeChain);
        int UpdateHasNoPublishedRevisionAndIsPublishedRevision(Guid id, string variant, bool? hasNoPublishedRevision,
                    bool? isPublishedRevision, int? hasNoPublishedRevisionIgnoreRevisionId = null, int? isPublishedRevisionIgnoreRevisionId = null);
        void Index(List<BaseModel> toIndex, bool addPublishInstruction = true, bool triggerEvents = true);
        DbParameter CreateParameter(string name, object value);
        public string GetSubStringFunctionForProvider();
        public string GetLengthFunctionForProvider();
        public string GetConcatOperatorForProvider();
        public string GetProviderPrefix();
    }
}