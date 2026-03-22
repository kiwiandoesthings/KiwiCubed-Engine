namespace KiwiCubed.Engine;

using System.Runtime.InteropServices;
using KiwiCubed.Api;
using Silk.NET.Core.Native;

public struct SivHandle {
	public readonly int id;
	public readonly int validityId;

	public SivHandle(int id, int validityId) {
		this.id = id;
		this.validityId = validityId;
	}
}

public class SivVector<T> where T : class {
	private struct Metadata {
		public T? data;
		public int validityId;
		public bool isAllocated;

		public Metadata(T? data, int validityId, bool isAllocated) {
			this.data = data;
			this.validityId = validityId;
			this.isAllocated = isAllocated;
		}
	}

	private List<Metadata> metadataList = new();
	private Stack<int> freeIndices = new();

	public int GetNextId() {
		if (freeIndices.Count > 0) {
			return freeIndices.Peek();
		}

		return metadataList.Count;
	}

	public SivHandle Add(T item) {
		int AUID;
		if (freeIndices.Count > 0) {
			AUID = freeIndices.Pop();
			ref Metadata meta = ref CollectionsMarshal.AsSpan(metadataList)[AUID];

			meta.data = item;
			meta.isAllocated = true;
		
			return new SivHandle(AUID, meta.validityId);
		}

		AUID = metadataList.Count;
		metadataList.Add(new Metadata(item, 0, true));

		return new SivHandle(AUID, 0);
	}

	public void Remove(int AUID) {
		if (AUID < 0 || AUID >= metadataList.Count) {
			return;
		}

		ref Metadata meta = ref CollectionsMarshal.AsSpan(metadataList)[AUID];
		if (!meta.isAllocated) {
			return;
		}

		meta.data = null;
		meta.isAllocated = false;
		meta.validityId++;

		freeIndices.Push(AUID);
	}

	public T? Get(int AUID) {
		if (AUID < 0 || AUID >= metadataList.Count) {
			return null;
		}

		ref Metadata meta = ref CollectionsMarshal.AsSpan(metadataList)[AUID];

		return meta.isAllocated ? meta.data : null;
	}

	public bool IsValid(SivHandle handle) {
		if (handle.id < 0 || handle.id >= metadataList.Count) {
			return false;
		}
		ref Metadata meta = ref CollectionsMarshal.AsSpan(metadataList)[handle.id];

		return meta.isAllocated && meta.validityId == handle.validityId;
	}

	public void ForEach(Action<T> function) {
		Span<Metadata> metadatas = CollectionsMarshal.AsSpan(metadataList);
		for (int iterator = 0; iterator < metadatas.Length; iterator++) {
			ref Metadata metadata = ref metadatas[iterator];
			if (metadata.data != null && metadata.isAllocated) {
				function(metadata.data);
			}
		}
	}
}