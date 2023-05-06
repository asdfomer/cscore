﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using com.csutil.json;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Zio;
using Newtonsoft.Json.Linq;

namespace com.csutil.model.ecs {

    public class EntityComponentSystem<T> where T : IEntityData {

        private readonly DirectoryEntry EntityDir;
        private readonly JsonDiffPatch JonDiffPatch = new JsonDiffPatch();

        /// <summary> A cache of all loaded templates as they are stored on disk, these need to be combined with all parents to get the full entity data </summary>
        private readonly Dictionary<string, JToken> LoadedTemplates = new Dictionary<string, JToken>();

        private Func<JsonSerializer> GetJsonSerializer = () => JsonSerializer.Create(JsonNetSettings.typedJsonSettings);

        public EntityComponentSystem(DirectoryEntry entityDir) {
            this.EntityDir = entityDir;
        }

        /// <summary> Loads all template files from disk into memory </summary>
        public async Task LoadAllTemplatesIntoMemory() {
            var jsonSerializer = GetJsonSerializer();
            var tasks = new List<Task>();
            foreach (var templateFile in EntityDir.EnumerateFiles()) {
                tasks.Add(TaskV2.Run((() => LoadJTokenFromFile(templateFile, jsonSerializer))));
            }
            await Task.WhenAll(tasks);
        }

        private void LoadJTokenFromFile(FileEntry templateFile, JsonSerializer jsonSerializer) {
            using (var stream = templateFile.OpenForRead()) {
                JToken template = jsonSerializer.Deserialize<JToken>(new JsonTextReader(new StreamReader(stream)));
                UpdateTemplateCache(templateFile.Name, template);
            }
        }

        private void UpdateTemplateCache(string id, JToken template) {
            LoadedTemplates[id] = template;
        }

        public void SaveAsTemplate(T instance) {
            var entityId = instance.GetId();
            entityId.ThrowErrorIfNullOrEmpty("entity.Id");
            var file = GetEntityFileForId(entityId);
            var json = ToJToken(instance, GetJsonSerializer());
            var templateId = instance.TemplateId;
            if (templateId != null) {
                var template = ComposeFullJson(templateId, allowLazyLoadFromDisk: true);
                json = JonDiffPatch.Diff(template, json);
            }
            file.SaveAsJson(json);
            UpdateTemplateCache(entityId, json);
        }

        private FileEntry GetEntityFileForId(string entityId) {
            return EntityDir.GetChild(entityId);
        }

        public void Delete(string entityId) {
            if (LoadedTemplates.Remove(entityId)) {
                GetEntityFileForId(entityId).DeleteV2();
            }
        }

        private JToken ToJToken(T instance, JsonSerializer serializer) {
            return JToken.FromObject(instance, serializer);
        }

        public T CreateVariantInstanceOf(T template) {
            var templateId = template.GetId();
            if (!LoadedTemplates.ContainsKey(templateId)) {
                throw new KeyNotFoundException("Template not found: " + templateId);
            }
            JsonSerializer serializer = GetJsonSerializer();
            var json = ToJToken(template, serializer);
            json["Id"] = "" + GuidV2.NewGuid();
            json["TemplateId"] = templateId;
            return ToObject(json, serializer);
        }

        private T ToObject(JToken json, JsonSerializer serializer) {
            T entity = json.ToObject<T>(serializer);
            AssertAllFieldsWereDeserialized(json, entity);
            return entity;
        }

        [Conditional("DEBUG")]
        private void AssertAllFieldsWereDeserialized(JToken sourceJson, T resultingEntity) {
            var backAsJson = ToJToken(resultingEntity, GetJsonSerializer());
            var diff = JonDiffPatch.Diff(sourceJson, backAsJson);
            if (diff != null) { throw new Exception("Not all props were deserialized, missing set/get for:" + diff); }
        }

        public IEnumerable<string> GetAllEntityIds() {
            return EntityDir.EnumerateFiles().Map(x => x.Name);
        }

        /// <summary> Creates a template instance based on the involved templates </summary>
        /// <param name="entityId"> The id of the entity to load </param>
        /// <param name="allowLazyLoadFromDisk"> if false its is expected all entities were already loaded into memory via <see cref="LoadAllTemplatesIntoMemory"/> </param>
        public T LoadTemplateInstance(string entityId, bool allowLazyLoadFromDisk = true) {
            return ToObject(ComposeFullJson(entityId, allowLazyLoadFromDisk), GetJsonSerializer());
        }

        /// <summary> Recursively composes the full json for the given entity id by applying the templates </summary>
        private JToken ComposeFullJson(string entityId, bool allowLazyLoadFromDisk) {
            if (!LoadedTemplates.ContainsKey(entityId)) {
                if (allowLazyLoadFromDisk) {
                    LoadJTokenFromFile(GetEntityFileForId(entityId), GetJsonSerializer());
                } else {
                    throw new KeyNotFoundException("Entity not found: " + entityId);
                }
            }
            var json = LoadedTemplates[entityId];
            if (json["TemplateId"] is JArray templateIdArray) {
                var templateId = templateIdArray[1].Value<string>();
                var template = ComposeFullJson(templateId, allowLazyLoadFromDisk);
                json = JonDiffPatch.Patch(template, json);
            }
            return json;
        }

    }

    public interface IEntityData : HasId {

        string Id { get; }
        string TemplateId { get; }
        Matrix4x4? LocalPose { get; }
        IList<IComponentData> Components { get; }
        IList<string> ChildrenIds { get; }
        IList<string> Tags { get; }

    }

    public interface IComponentData : HasId {

    }

}