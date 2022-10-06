using DesktopUI2;
using DesktopUI2.Models;
using DesktopUI2.Models.Filters;
using DesktopUI2.Models.Settings;
using DesktopUI2.ViewModels;
using Dlubal;
using Objects.Converter.DLUBAL;
using Speckle.Core.Api;
using Speckle.Core.Kits;
using Speckle.Core.Models;
using Speckle.Core.Transports;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Speckle.ConnectorDLUBAL
{
    internal class ConnectorBindingsDlubal : ConnectorBindings, Dlubal.IMainApp
    {
        private static Dlubal.DlubalWSHandler? dlubalWSHandler = null;

        public override bool CanPreviewSend => false;

        public override bool CanPreviewReceive => false;

        public override string GetActiveViewName()
        {
            throw new NotImplementedException();
        }

        public override List<MenuItem> GetCustomStreamMenuItems()
        {
            return new List<MenuItem>();
        }

        public override string GetDocumentId()
        {
            Dlubal.DlubalWSHandler handler = GetWSHandler();
            Dlubal.ModelHandler? model = handler.GetModel(Dlubal.DlubalWSHandler.ModelSelection.Active);
            if (model is null)
            {
                return "";
            }

            return model.GetModelName();

        }

        public override string GetDocumentLocation()
        {
            Dlubal.DlubalWSHandler handler = GetWSHandler();
            Dlubal.ModelHandler? model = handler.GetModel(Dlubal.DlubalWSHandler.ModelSelection.Active);
            if (model is null)
            {
                return "";
            }

            return model.GetModelPath();
        }

        public override string GetFileName()
        {
            FileInfo info = new(GetDocumentLocation());
            return info.Name;
        }

        public override string GetHostAppName()
        {
            return Dlubal.DlubalWSHandler.ApplicationName;
        }

        public override string GetHostAppNameVersion()
        {
            return Dlubal.DlubalWSHandler.ApplicationName;
        }

        public override List<string> GetObjectsInView()
        {
            return new List<string>();
        }

        public override List<ReceiveMode> GetReceiveModes()
        {
            return new List<ReceiveMode> {  ReceiveMode.Create, ReceiveMode.Update };
        }

        public override List<string> GetSelectedObjects()
        {
            return new List<string>();
        }

        public override List<ISelectionFilter> GetSelectionFilters()
        {
            return new List<ISelectionFilter>()
            {
                new AllSelectionFilter { Slug = "all", Name = "Everything", Icon = "CubeScan", Description = "Selects all document objects and project info." },
            };
        }

        public override List<ISetting> GetSettings()
        {
            return new();
        }

        public override List<StreamState> GetStreamsInFile()
        {
            throw new NotImplementedException();
        }

        public void Log(string messageType, string message)
        {
            throw new NotImplementedException();
        }

        public override async Task<StreamState> ReceiveStream(StreamState state, ProgressViewModel progress)
        {
            var kit = KitManager.GetDefaultKit();
            var converter = kit.LoadConverter(ConverterDlubal.ServicedAppName);
            DlubalWSHandler handler = GetWSHandler();
            ModelHandler? modelHandler = handler.GetModel(DlubalWSHandler.ModelSelection.ActiveOrNew, state.UserId);
            converter.SetContextDocument(modelHandler);

            if (converter == null)
            {
                progress.CancellationTokenSource.Cancel();
                throw new Exception("Could not find any Kit!");
            }

            var transport = new ServerTransport(state.Client.Account, state.StreamId);

            if (progress.CancellationTokenSource.Token.IsCancellationRequested)
                return null;

            Commit? commit = null;
            if (state.CommitId == "latest")
            {
                var res = await state.Client.BranchGet(progress.CancellationTokenSource.Token, state.StreamId, state.BranchName, 1);
                commit = res.commits.items.FirstOrDefault();

            }
            else
            {
                var rest = await state.Client.CommitGet(progress.CancellationTokenSource.Token, state.StreamId, state.CommitId);
                commit = rest;
            }

            if (commit == null || progress.CancellationTokenSource.Token.IsCancellationRequested)
            {
                return null;
            }

            string referencedObject = commit.referencedObject;
            var context = SynchronizationContext.Current;

            var commitObject = await Operations.Receive(
                referencedObject,
                progress.CancellationTokenSource.Token,
                transport,
                onProgressAction: dict => progress.Update(dict),
                onErrorAction: (s, e) =>
                {
                    progress.Report.LogOperationError(e);
                    progress.CancellationTokenSource.Cancel();
                },
                onTotalChildrenCountKnown: (c) => progress.Max = c,
                disposeTransports: true
                );

            //if (progress.Report.OperationErrorsCount != 0)
            //{
            //    return state;
            //}

            if (progress.CancellationTokenSource.Token.IsCancellationRequested)
            {
                return null;
            }

            var conversionProgressDict = new ConcurrentDictionary<string, int>();
            conversionProgressDict["Conversion"] = 0;
            var commitLayerName = DesktopUI2.Formatting.CommitInfo(state.CachedStream.name, state.BranchName, commit.id);

            int count = 0;
            var commitObjs = GetSpeckleObjectsFromCommit(commitObject, converter, commitLayerName, state, ref count);

            foreach (var tuple in commitObjs)
            {
                var (obj, layerPath) = tuple;
                BakeObject(obj, layerPath, state, converter);
                if (progress.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    return null;
                }

                conversionProgressDict["Conversion"]++;
                progress.Update(conversionProgressDict);
            }

            modelHandler?.WriteCache(ModelHandler.SupportedObjectTypes);
            handler.Disconnect();

            return state;
        }

        private void BakeObject(Base obj, string layerPath, StreamState state, ISpeckleConverter converter)
        {
            var converted = converter.ConvertToNative(obj);
            if (converted == null)
            {
                var exception = new Exception($"Failed to convert object {obj.id} of type {obj.speckle_type}.");
                converter.Report.LogConversionError(exception);
                return;
            }

            var convertedList = new List<object>();

            void FlattenConvertedObject(object item)
            {
                if (item is IList list)
                {
                    foreach (object child in list)
                    {
                        FlattenConvertedObject(child);
                    }
                }
                else
                {
                    convertedList?.Add(item);
                }
            }

            FlattenConvertedObject(converted);
        }

        private List<Tuple<Base, string>> GetSpeckleObjectsFromCommit(object obj, ISpeckleConverter converter, string layer, StreamState state, ref int count, bool foundConvertibleMember = false)
        {
            var result = new List<Tuple<Base, string>>();
            if (obj is Base @base)
            {
                if (converter.CanConvertToNative(@base))
                {
                    result.Add(new(@base, layer));
                    return result;
                }
                else
                {
                    List<string> props = @base.GetDynamicMembers().ToList();
                    if (@base.GetMembers().ContainsKey("displayValue"))
                        props.Add("displayValue");
                    else if (@base.GetMembers().ContainsKey("displayMesh")) // add display mesh to member list if it exists. this will be deprecated soon
                        props.Add("displayMesh");
                    if (@base.GetMembers().ContainsKey("elements")) // this is for builtelements like roofs, walls, and floors.
                        props.Add("elements");
                    int totalMembers = props.Count;

                    foreach (var prop in props)
                    {
                        count++;

                        // get bake layer name
                        string objLayerName = prop.StartsWith("@") ? prop.Remove(0, 1) : prop;
                        string rhLayerName = objLayerName;

                        var nestedObjects = GetSpeckleObjectsFromCommit(@base[prop], converter, rhLayerName, state, ref count, foundConvertibleMember);
                        if (nestedObjects.Count > 0)
                        {
                            result.AddRange(nestedObjects);
                            foundConvertibleMember = true;
                        }
                    }

                    if (!foundConvertibleMember && count == totalMembers) // this was an unsupported geo
                        converter.Report.Log($"Skipped not supported type: { @base.speckle_type }. Object {@base.id} not baked.");

                    return result;
                }
            }

            if (obj is IReadOnlyList<object> list)
            {
                count = 0;
                foreach (var listObj in list)
                    result.AddRange(GetSpeckleObjectsFromCommit(listObj, converter, layer, state, ref count));
                return result;
            }

            if (obj is IDictionary dict)
            {
                count = 0;
                foreach (DictionaryEntry kvp in dict)
                    result.AddRange(GetSpeckleObjectsFromCommit(kvp.Value, converter, layer, state, ref count));
                return result;
            }

            return result;
        }

        public override async Task<string> SendStream(StreamState state, ProgressViewModel progress)
        {
            var kit = KitManager.GetDefaultKit();

            var converter = kit.LoadConverter(ConverterDlubal.ServicedAppName);

            Dlubal.DlubalWSHandler handler = GetWSHandler();
            Dlubal.ModelHandler? modelHandler = handler.GetModel(Dlubal.DlubalWSHandler.ModelSelection.Active);
            if (modelHandler == null)
            {
                return null;
            }

            var commitObject = new Base();
            Base? converted = null;
            int objCount = 0;

            foreach (var type in Dlubal.ModelHandler.SupportedObjectTypes)
            {
                modelHandler.LoadObjectsToCache(type);
                foreach (var obj in modelHandler.GetObjects(type))
                {
                    if (converter.CanConvertToSpeckle(obj))
                    {
                        converted = converter.ConvertToSpeckle(obj);

                        if (!commitObject.GetDynamicMemberNames().Contains(obj.CollectionName))
                        {
                            commitObject[obj.CollectionName] = new List<Base>();
                        }

                        (commitObject[obj.CollectionName] as List<Base>)?.Add(converted);
                        objCount++;
                    }
                }
            }

            var objectId = await Operations.Send(
                @object: commitObject,
                cancellationToken: progress.CancellationTokenSource.Token);

            var actualCommit = new CommitCreateInput
            {
                streamId = state.StreamId,
                objectId = objectId,
                branchName = state.BranchName,
                message = state.CommitMessage != null ? state.CommitMessage : $"Pushed {objCount} elements from {Dlubal.DlubalWSHandler.ApplicationName}.",
                sourceApplication = Dlubal.DlubalWSHandler.ApplicationName
            };

            if (state.PreviousCommitId != null) { actualCommit.parents = new List<string>() { state.PreviousCommitId }; }

            try
            {
                var commitId = await state.Client.CommitCreate(actualCommit);
                state.PreviousCommitId = commitId;
                return commitId;
            }
            catch (Exception e)
            {
                progress.Report.LogOperationError(e);
            }
            return null;

        }

        public override void WriteStreamsToFile(List<StreamState> streams)
        {

        }

        private Dlubal.DlubalWSHandler GetWSHandler()
        {
            if (dlubalWSHandler == null)
            {
                ConnectToDlubalApplication();
            }
#pragma warning disable CS8603 // Not null here.
            return dlubalWSHandler;
#pragma warning restore CS8603
        }

        private bool ConnectToDlubalApplication()
        {
            if (dlubalWSHandler == null)
            {
                Dlubal.DlubalWSHandler handler = new Dlubal.DlubalWSHandler(this);
                if (!handler.IsDlubalApplicationRunning())
                {
                    handler.StartApplication();
                }
                dlubalWSHandler = handler;
                return handler.Connect();
            }
            return true;
        }

        public override void ResetDocument()
        {
            //throw new NotImplementedException();
        }

        public override void PreviewSend(StreamState state, ProgressViewModel progress)
        {
            throw new NotImplementedException();
        }

        public override Task<StreamState> PreviewReceive(StreamState state, ProgressViewModel progress)
        {
            throw new NotImplementedException();
        }

        public override void SelectClientObjects(List<string> objs, bool deselect = false)
        {
            throw new NotImplementedException();
        }

        public override Task<Dictionary<string, List<MappingViewModel.MappingValue>>> ImportFamilyCommand(Dictionary<string, List<MappingViewModel.MappingValue>> Mapping)
        {
            throw new NotImplementedException();
        }
    }
}
