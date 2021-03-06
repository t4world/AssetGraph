using UnityEngine;

using System;
using System.IO;

namespace AssetBundleGraph {
	public class InternalAssetData {
		public readonly string traceId;
		public readonly string absoluteSourcePath;
		public readonly string sourceBasePath;
		public readonly string fileNameAndExtension;
		public readonly string pathUnderSourceBase;
		public readonly string importedPath;
		public readonly string pathUnderConnectionId;
		public readonly string exportedPath;
		public readonly string assetId;
		public readonly Type assetType;
		public readonly bool isNew;
		
		public bool isBundled;
		
		
		/**
			new assets which is Loaded by Loader.
		*/
		public static InternalAssetData InternalImportedAssetDataByLoader (string absoluteSourcePath, string sourceBasePath, string importedPath, string assetId, Type assetType) {
			return new InternalAssetData(
				traceId:Guid.NewGuid().ToString(),
				absoluteSourcePath:absoluteSourcePath,
				sourceBasePath:sourceBasePath,
				fileNameAndExtension:Path.GetFileName(absoluteSourcePath),
				pathUnderSourceBase:GetPathWithoutBasePath(absoluteSourcePath, sourceBasePath),
				importedPath:importedPath,
				pathUnderConnectionId:The2LevelLowerPath(importedPath),
				assetId:assetId,
				assetType:assetType
			);
		}

		/**
			new assets which is generated through ImportSettings.
		*/
		public static InternalAssetData InternalAssetDataByImporter (string traceId, string absoluteSourcePath, string sourceBasePath, string fileNameAndExtension, string pathUnderSourceBase, string importedPath, string assetId, Type assetType) {
			return new InternalAssetData(
				traceId:traceId,
				absoluteSourcePath:absoluteSourcePath,
				sourceBasePath:sourceBasePath,
				fileNameAndExtension:fileNameAndExtension,
				pathUnderSourceBase:pathUnderSourceBase,
				importedPath:importedPath,
				pathUnderConnectionId:The2LevelLowerPath(importedPath),
				assetId:assetId,
				assetType:assetType
			);
		}

		/**
			new assets which is generated on Imported or Prefabricated.
		*/
		public static InternalAssetData InternalAssetDataGeneratedByImporterOrModifierOrPrefabricator (string importedPath, string assetId, Type assetType, bool isNew, bool isBundled) {
			return new InternalAssetData(
				traceId:Guid.NewGuid().ToString(),
				fileNameAndExtension:Path.GetFileName(importedPath),
				importedPath:importedPath,
				pathUnderConnectionId:The2LevelLowerPath(importedPath),
				assetId:assetId,
				assetType:assetType,
				isNew:isNew,
				isBundled:isBundled
			);
		}

		/**
			new assets which is generated on Bundlized.
			no file exists. only setting applyied.
		*/
		public static InternalAssetData InternalAssetDataGeneratedByBundlizer (string importedPath) {
			return new InternalAssetData(
				traceId:Guid.NewGuid().ToString(),
				fileNameAndExtension:Path.GetFileName(importedPath),
				importedPath:importedPath,
				pathUnderConnectionId:The2LevelLowerPath(importedPath)
			);
		}
		
		public static InternalAssetData InternalAssetDataGeneratedByBundleBuilder (string importedPath) {
			return new InternalAssetData(
				traceId:Guid.NewGuid().ToString(),
				fileNameAndExtension:Path.GetFileName(importedPath),
				importedPath:importedPath,
				pathUnderConnectionId:The2LevelLowerPath(importedPath)
			);
		}

		public static InternalAssetData InternalAssetDataGeneratedByExporter (string exportedPath) {
			return new InternalAssetData(
				traceId:Guid.NewGuid().ToString(),
				fileNameAndExtension:Path.GetFileName(exportedPath),
				exportedPath:exportedPath
			);
		}


		private InternalAssetData (
			string traceId = null,
			string absoluteSourcePath = null,
			string sourceBasePath = null,
			string fileNameAndExtension = null,
			string pathUnderSourceBase = null,
			string importedPath = null,
			string pathUnderConnectionId = null,
			string exportedPath = null,
			string assetId = null,
			Type assetType = null,
			bool isNew = false,
			bool isBundled = false
		) {
			this.traceId = traceId;
			this.absoluteSourcePath = absoluteSourcePath;
			this.sourceBasePath = sourceBasePath;
			this.fileNameAndExtension = fileNameAndExtension;
			this.pathUnderSourceBase = pathUnderSourceBase;
			this.importedPath = importedPath;
			this.pathUnderConnectionId = pathUnderConnectionId;
			this.exportedPath = exportedPath;
			this.assetId = assetId;
			this.assetType = assetType;
			this.isNew = isNew;
			this.isBundled = isBundled;
		}

		/**
			get ITEM_FOLDERS&ITEMS path
			from Assets/AssetBundleGraph/Cache/NODE_KIND/CONNECTION_ID_FOLDER/ITEM_FOLDERS&ITEMS path.

			e.g. 
				Assets/AssetBundleGraph/Cache/NODE_KIND_FOLDER/CONNECTION_ID_FOLDER/somewhere/something.png
				->
				something/something.png
		*/
		private static string The2LevelLowerPath (string assetsTemp_ConnectionId_ResourcePath) {
			var splitted = assetsTemp_ConnectionId_ResourcePath.Split(AssetBundleGraphSettings.UNITY_FOLDER_SEPARATOR);
			var depthCount = AssetBundleGraphSettings.APPLICATIONDATAPATH_CACHE_PATH.Split(AssetBundleGraphSettings.UNITY_FOLDER_SEPARATOR).Length + 1;// last +1 is connectionId's count
			var concatenated = new string[splitted.Length - depthCount];
			Array.Copy(splitted, depthCount, concatenated, 0, concatenated.Length);
			var resultPath = string.Join(AssetBundleGraphSettings.UNITY_FOLDER_SEPARATOR.ToString(), concatenated);
			
			return resultPath;
		}

		public static string GetPathWithoutBasePath (string localPathWithBasePath, string basePath) {
			var replaced = localPathWithBasePath.Replace(basePath, string.Empty);
			if (replaced.StartsWith(AssetBundleGraphSettings.UNITY_FOLDER_SEPARATOR.ToString())) return replaced.Substring(1);
			return replaced;
		}
		
		public static string GetPathWithBasePath (string localPathWithoutBasePath, string basePath) {
			return FileController.PathCombine(basePath, localPathWithoutBasePath);
		}

		public string GetAbsolutePathOrImportedPath () {
			if (absoluteSourcePath != null) return absoluteSourcePath;
			return importedPath;
		}
	}
}