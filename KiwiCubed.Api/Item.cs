namespace KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public interface IItem {
	public MetaTexture GetTexture();
	public int GetStackSize();
}