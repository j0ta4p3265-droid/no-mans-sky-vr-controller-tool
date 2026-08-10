#pragma once
#include "generated/interfaces/vrtypes.h"
#include "Misc/xr_ext.h"
#include <functional>
#include <string>
#include <vector>

class NmsVulkanKeyboard {
public:
	using EventDispatch = std::function<void(vr::VREvent_t)>;
	using TextChanged = std::function<void(const std::string&)>;
	NmsVulkanKeyboard(const vr::VRVulkanTextureData_t&, uint64_t, uint32_t, const char*, EventDispatch, TextChanged);
	~NmsVulkanKeyboard();
	const std::vector<XrCompositionLayerBaseHeader*>& Update();
	void HandleInput(vr::TrackedDeviceIndex_t deviceIndex, const vr::VRControllerState_t& state);
	bool IsClosed() const { return closed; }
	static bool IsActive() { return active; }
	void Close(bool submit);
private:
	void createResources(); void destroyResources(); void render(); void upload(); void pollInput(); void activate();
	void sendEvent(vr::EVREventType, const char* utf8 = nullptr);
	uint32_t memoryType(uint32_t, VkMemoryPropertyFlags) const;
	vr::VRVulkanTextureData_t gfx{}; uint64_t userValue=0; uint32_t maxLength=0;
	EventDispatch dispatch; TextChanged changed; std::string text;
	bool closed=false, dirty=true, shift=false; bool wasPressed[2]{false,false}; int selected=0; double nextMove=0;
	static constexpr uint32_t width=1024, height=512;
	std::vector<uint8_t> pixels; std::vector<std::string> keys; std::vector<int> rowStart, rowCount;
	XrSwapchain swapchain=XR_NULL_HANDLE; std::vector<XrSwapchainImageVulkanKHR> images;
	XrCompositionLayerQuad layer{XR_TYPE_COMPOSITION_LAYER_QUAD}; std::vector<XrCompositionLayerBaseHeader*> layers;
	VkBuffer staging=VK_NULL_HANDLE; VkDeviceMemory stagingMemory=VK_NULL_HANDLE;
	VkCommandPool commandPool=VK_NULL_HANDLE; VkCommandBuffer commandBuffer=VK_NULL_HANDLE;
	static bool active;
};
