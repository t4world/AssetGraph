using UnityEngine;

using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;


namespace AssetGraph {
	public class GraphStack {

		public struct EndpointNodeIdsAndNodeDatasAndConnectionDatas {
			public List<string> endpointNodeIds;
			public List<NodeData> nodeDatas;
			public List<ConnectionData> connectionDatas;

			public EndpointNodeIdsAndNodeDatasAndConnectionDatas (List<string> endpointNodeIds, List<NodeData> nodeDatas, List<ConnectionData> connectionDatas) {
				this.endpointNodeIds = endpointNodeIds;
				this.nodeDatas = nodeDatas;
				this.connectionDatas = connectionDatas;
			}
		}
		

		public void RunStackedGraph (Dictionary<string, object> graphDataDict) {
			var EndpointNodeIdsAndNodeDatasAndConnectionDatas = SerializeNodeRoute(graphDataDict);
			
			var endpointNodeIds = EndpointNodeIdsAndNodeDatasAndConnectionDatas.endpointNodeIds;
			var nodeDatas = EndpointNodeIdsAndNodeDatasAndConnectionDatas.nodeDatas;
			var connectionDatas = EndpointNodeIdsAndNodeDatasAndConnectionDatas.connectionDatas;

			foreach (var endNodeId in endpointNodeIds) {
				RunSerializedRoute(endNodeId, nodeDatas, connectionDatas);
			}
		}
		
		/**
			GUI上に展開されているConnectionsから、接続要素の直列化を行う。
			末尾の数だけ列が作られる。
			列の中身の精査はしない。
				・ループチェックしてない
				・不要なデータも入ってる
		*/
		public EndpointNodeIdsAndNodeDatasAndConnectionDatas SerializeNodeRoute (Dictionary<string, object> graphDataDict) {
			Debug.LogWarning("Endの条件を絞れば、不要な、たとえばExportではないNodeが末尾であれば無視する、とか警告だすとかができるはず。");
			var nodeIds = new List<string>();
			var nodesSource = graphDataDict[AssetGraphSettings.ASSETGRAPH_DATA_NODES] as List<object>;
			
			var connectionsSource = graphDataDict[AssetGraphSettings.ASSETGRAPH_DATA_CONNECTIONS] as List<object>;
			var connections = new List<ConnectionData>();
			foreach (var connectionSource in connectionsSource) {
				var connectionDict = connectionSource as Dictionary<string, object>;
				
				var connectionId = connectionDict[AssetGraphSettings.CONNECTION_ID] as string;
				var connectionLabel = connectionDict[AssetGraphSettings.CONNECTION_LABEL] as string;
				var fromNodeId = connectionDict[AssetGraphSettings.CONNECTION_FROMNODE] as string;
				var toNodeId = connectionDict[AssetGraphSettings.CONNECTION_TONODE] as string;
				connections.Add(new ConnectionData(connectionId, connectionLabel, fromNodeId, toNodeId));
			}

			var nodeDatas = new List<NodeData>();

			foreach (var nodeSource in nodesSource) {
				var nodeDict = nodeSource as Dictionary<string, object>;
				var nodeId = nodeDict[AssetGraphSettings.NODE_ID] as string;
				nodeIds.Add(nodeId);

				var kindSource = nodeDict[AssetGraphSettings.NODE_KIND] as string;
				var kind = AssetGraphSettings.NodeKindFromString(kindSource);
				var scriptType = nodeDict[AssetGraphSettings.NODE_CLASSNAME] as string;

				switch (kind) {
					case AssetGraphSettings.NodeKind.LOADER: {
						var loadFilePath = nodeDict[AssetGraphSettings.LOADERNODE_FILE_PATH] as string;
						nodeDatas.Add(new NodeData(nodeId, kind, scriptType, loadFilePath));
						break;
					}
					default: {
						nodeDatas.Add(new NodeData(nodeId, kind, scriptType));
						break;
					}
				}
			}
			
			/*
				collect node's child. for detecting endpoint of relationship.
			*/
			var nodeIdListWhichHasChild = new List<string>();

			foreach (var connection in connections) {
				nodeIdListWhichHasChild.Add(connection.fromNodeId);
			}
			var noChildNodeIds = nodeIds.Except(nodeIdListWhichHasChild).ToList();

			/*
				adding parentNode id x n into childNode for run up relationship from childNode.
			*/
			foreach (var connection in connections) {
				// collect parent Ids into child node.
				var targetNodes = nodeDatas.Where(nodeData => nodeData.currentNodeId == connection.toNodeId).ToList();
				foreach (var targetNode in targetNodes) targetNode.AddConnectionData(connection);
			}
			
			return new EndpointNodeIdsAndNodeDatasAndConnectionDatas(noChildNodeIds, nodeDatas, connections);
		}

		/**
			直列化された要素を実行する
		*/
		public List<string> RunSerializedRoute (string endNodeId, List<NodeData> nodeDatas, List<ConnectionData> connections) {
			var resultDict = new Dictionary<string, List<AssetData>>();
			RunUpToParent(endNodeId, nodeDatas, connections, resultDict);

			return resultDict.Keys.ToList();
		}

		/**
			ノードの親を辿り実行、ConnectionIdごとの結果を収集する	
		*/
		private void RunUpToParent (string nodeId, List<NodeData> nodeDatas, List<ConnectionData> connectionDatas, Dictionary<string, List<AssetData>> resultDict) {
			var currentNodeDatas = nodeDatas.Where(relation => relation.currentNodeId == nodeId).ToList();
			if (!currentNodeDatas.Any()) throw new Exception("failed to find node from relations. nodeId:" + nodeId);

			var currentNodeData = currentNodeDatas[0];

			/*
				run parent nodes of this node.
			*/
			var parentNodeIds = currentNodeData.connectionDataOfParents.Select(conData => conData.fromNodeId).ToList();
			foreach (var parentNodeId in parentNodeIds) {
				RunUpToParent(parentNodeId, nodeDatas, connectionDatas, resultDict);
			}

			var connectionLabelsFromThisNodeToChildNode = connectionDatas
				.Where(con => con.fromNodeId == nodeId)
				.Select(con => con.connectionLabel)
				.ToList();

			/*
				this is label of connection.

				will be ignored in Filter node,
				because the Filter node will generate new label of connection by itself.
			*/
			var labelToChild = string.Empty;
			if (connectionLabelsFromThisNodeToChildNode.Any()) {
				labelToChild = connectionLabelsFromThisNodeToChildNode[0];
			} else {
				Debug.LogWarning("this node is endpoint. no next node and no next result,,,ちょっと整理が必要");
			}

			var classStr = currentNodeData.currentNodeClassStr;
			var nodeKind = currentNodeData.currentNodeKind;
			
			var inputParentResults = new List<AssetData>();
			
			var receivingConnectionIds = connectionDatas
				.Where(con => con.toNodeId == nodeId)
				.Select(con => con.connectionId)
				.ToList();

			foreach (var connecionId in receivingConnectionIds) {
				var result = resultDict[connecionId];
				inputParentResults.AddRange(result);
			}

			Action<string, string, List<AssetData>> Output = (string dataSourceNodeId, string connectionLabel, List<AssetData> source) => {				
				var targetConnectionIds = connectionDatas
					.Where(con => con.fromNodeId == dataSourceNodeId) // from this node
					.Where(con => con.connectionLabel == connectionLabel) // from this label
					.Select(con => con.connectionId)
					.ToList();
				
				if (!targetConnectionIds.Any()) {
					Debug.LogWarning("this dataSourceNodeId:" + dataSourceNodeId + " is endpointint");
					return;
				}

				var targetConnectionId = targetConnectionIds[0];
				resultDict[targetConnectionId] = source;
			};

			switch (nodeKind) {
				case AssetGraphSettings.NodeKind.LOADER: {
					var executor = Executor<IntegratedLoader>(classStr);
					executor.loadFilePath = currentNodeData.loadFilePath;
					executor.Run(nodeId, labelToChild, inputParentResults, Output);
					break;
				}
				case AssetGraphSettings.NodeKind.FILTER: {
					var executor = Executor<FilterBase>(classStr);
					executor.Run(nodeId, labelToChild, inputParentResults, Output);
					break;
				}
				case AssetGraphSettings.NodeKind.IMPORTER: {
					var executor = Executor<ImporterBase>(classStr);
					executor.Run(nodeId, labelToChild, inputParentResults, Output);
					break;
				}
				case AssetGraphSettings.NodeKind.PREFABRICATOR: {
					Debug.LogError("not yet applied node kind, Prefabricator");
					break;
				}
				case AssetGraphSettings.NodeKind.BUNDLIZER: {
					Debug.LogError("not yet applied node kind, Bundlizer");
					break;
				}
				case AssetGraphSettings.NodeKind.EXPORTER: {
					Debug.LogError("not yet applied node kind, Exporter");
					break;
				}
			}
		}

		public T Executor<T> (string classStr) where T : INodeBase {
			var nodeScriptTypeStr = classStr;
			var nodeScriptInstance = Assembly.GetExecutingAssembly().CreateInstance(nodeScriptTypeStr);
			if (nodeScriptInstance == null) throw new Exception("failed to generate class information of class:" + nodeScriptTypeStr);
			return ((T)nodeScriptInstance);
		}
	}


	public class NodeData {
		public readonly string currentNodeId;
		public readonly AssetGraphSettings.NodeKind currentNodeKind;
		public readonly string currentNodeClassStr;
		public List<ConnectionData> connectionDataOfParents = new List<ConnectionData>();

		// for Loader
		public readonly string loadFilePath;

		/**
			constructor for Loader
		*/
		public NodeData (string currentNodeId, AssetGraphSettings.NodeKind currentNodeKind, string currentNodeClassStr, string loadFilePath) {
			this.currentNodeId = currentNodeId;
			this.currentNodeKind = currentNodeKind;
			this.currentNodeClassStr = currentNodeClassStr;
			this.loadFilePath = loadFilePath;
		}

		/**
			constructor for Filter, Importer
		*/
		public NodeData (string currentNodeId, AssetGraphSettings.NodeKind currentNodeKind, string currentNodeClassStr) {
			this.currentNodeId = currentNodeId;
			this.currentNodeKind = currentNodeKind;
			this.currentNodeClassStr = currentNodeClassStr;
			this.loadFilePath = null;
		}

		public void AddConnectionData (ConnectionData connection) {
			connectionDataOfParents.Add(new ConnectionData(connection));
		}
	}

	public class ConnectionData {
		public readonly string connectionId;
		public readonly string connectionLabel;
		public readonly string fromNodeId;
		public readonly string toNodeId;

		public ConnectionData (string connectionId, string connectionLabel, string fromNodeId, string toNodeId) {
			this.connectionId = connectionId;
			this.connectionLabel = connectionLabel;
			this.fromNodeId = fromNodeId;
			this.toNodeId = toNodeId;
		}

		public ConnectionData (ConnectionData connection) {
			this.connectionId = connection.connectionId;
			this.connectionLabel = connection.connectionLabel;
			this.fromNodeId = connection.fromNodeId;
			this.toNodeId = connection.toNodeId;
		}
	}
}