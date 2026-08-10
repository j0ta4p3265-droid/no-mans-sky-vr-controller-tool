#define XR_USE_PLATFORM_WIN32

#include <openxr/openxr.h>
#include <openvr.h>

#include <Windows.h>

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>

namespace {

struct Vec3 {
    double x = 0.0;
    double y = 0.0;
    double z = 0.0;
};

struct Quat {
    double x = 0.0;
    double y = 0.0;
    double z = 0.0;
    double w = 1.0;
};

struct Mat4 {
    double m[4][4]{};
};

struct CorrectionSample {
    Vec3 position;
    Quat orientation;
};

Mat4 Identity() {
    Mat4 result{};
    for (int i = 0; i < 4; ++i) {
        result.m[i][i] = 1.0;
    }
    return result;
}

Mat4 Multiply(const Mat4& a, const Mat4& b) {
    Mat4 result{};
    for (int row = 0; row < 4; ++row) {
        for (int column = 0; column < 4; ++column) {
            for (int k = 0; k < 4; ++k) {
                result.m[row][column] += a.m[row][k] * b.m[k][column];
            }
        }
    }
    return result;
}

Mat4 InverseRigid(const Mat4& input) {
    Mat4 result = Identity();
    for (int row = 0; row < 3; ++row) {
        for (int column = 0; column < 3; ++column) {
            result.m[row][column] = input.m[column][row];
        }
    }
    for (int row = 0; row < 3; ++row) {
        result.m[row][3] = -(result.m[row][0] * input.m[0][3] +
                             result.m[row][1] * input.m[1][3] +
                             result.m[row][2] * input.m[2][3]);
    }
    return result;
}

Mat4 FromOpenVr(const vr::HmdMatrix34_t& input) {
    Mat4 result = Identity();
    for (int row = 0; row < 3; ++row) {
        for (int column = 0; column < 4; ++column) {
            result.m[row][column] = input.m[row][column];
        }
    }
    return result;
}

Mat4 FromOpenXr(const XrPosef& pose) {
    const double x = pose.orientation.x;
    const double y = pose.orientation.y;
    const double z = pose.orientation.z;
    const double w = pose.orientation.w;

    Mat4 result = Identity();
    result.m[0][0] = 1.0 - 2.0 * (y * y + z * z);
    result.m[0][1] = 2.0 * (x * y - z * w);
    result.m[0][2] = 2.0 * (x * z + y * w);
    result.m[1][0] = 2.0 * (x * y + z * w);
    result.m[1][1] = 1.0 - 2.0 * (x * x + z * z);
    result.m[1][2] = 2.0 * (y * z - x * w);
    result.m[2][0] = 2.0 * (x * z - y * w);
    result.m[2][1] = 2.0 * (y * z + x * w);
    result.m[2][2] = 1.0 - 2.0 * (x * x + y * y);
    result.m[0][3] = pose.position.x;
    result.m[1][3] = pose.position.y;
    result.m[2][3] = pose.position.z;
    return result;
}

Quat MatrixToQuat(const Mat4& matrix) {
    Quat q{};
    const double trace = matrix.m[0][0] + matrix.m[1][1] + matrix.m[2][2];
    if (trace > 0.0) {
        const double s = std::sqrt(trace + 1.0) * 2.0;
        q.w = 0.25 * s;
        q.x = (matrix.m[2][1] - matrix.m[1][2]) / s;
        q.y = (matrix.m[0][2] - matrix.m[2][0]) / s;
        q.z = (matrix.m[1][0] - matrix.m[0][1]) / s;
    } else if (matrix.m[0][0] > matrix.m[1][1] && matrix.m[0][0] > matrix.m[2][2]) {
        const double s = std::sqrt(1.0 + matrix.m[0][0] - matrix.m[1][1] - matrix.m[2][2]) * 2.0;
        q.w = (matrix.m[2][1] - matrix.m[1][2]) / s;
        q.x = 0.25 * s;
        q.y = (matrix.m[0][1] + matrix.m[1][0]) / s;
        q.z = (matrix.m[0][2] + matrix.m[2][0]) / s;
    } else if (matrix.m[1][1] > matrix.m[2][2]) {
        const double s = std::sqrt(1.0 + matrix.m[1][1] - matrix.m[0][0] - matrix.m[2][2]) * 2.0;
        q.w = (matrix.m[0][2] - matrix.m[2][0]) / s;
        q.x = (matrix.m[0][1] + matrix.m[1][0]) / s;
        q.y = 0.25 * s;
        q.z = (matrix.m[1][2] + matrix.m[2][1]) / s;
    } else {
        const double s = std::sqrt(1.0 + matrix.m[2][2] - matrix.m[0][0] - matrix.m[1][1]) * 2.0;
        q.w = (matrix.m[1][0] - matrix.m[0][1]) / s;
        q.x = (matrix.m[0][2] + matrix.m[2][0]) / s;
        q.y = (matrix.m[1][2] + matrix.m[2][1]) / s;
        q.z = 0.25 * s;
    }
    return q;
}

double Dot(const Quat& a, const Quat& b) {
    return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
}

Quat Normalize(Quat q) {
    const double length = std::sqrt(Dot(q, q));
    if (length > 0.0) {
        q.x /= length;
        q.y /= length;
        q.z /= length;
        q.w /= length;
    }
    return q;
}

Mat4 FromQuatAndPosition(const Quat& q, const Vec3& p) {
    XrPosef pose{{static_cast<float>(q.x), static_cast<float>(q.y), static_cast<float>(q.z), static_cast<float>(q.w)},
                 {static_cast<float>(p.x), static_cast<float>(p.y), static_cast<float>(p.z)}};
    return FromOpenXr(pose);
}

Vec3 MatrixEulerDegrees(const Mat4& matrix) {
    constexpr double radiansToDegrees = 57.295779513082320876;
    Vec3 result{};
    const double sy = std::sqrt(matrix.m[0][0] * matrix.m[0][0] + matrix.m[1][0] * matrix.m[1][0]);
    const bool singular = sy < 1e-6;
    if (!singular) {
        result.x = std::atan2(matrix.m[2][1], matrix.m[2][2]);
        result.y = std::atan2(-matrix.m[2][0], sy);
        result.z = std::atan2(matrix.m[1][0], matrix.m[0][0]);
    } else {
        result.x = std::atan2(-matrix.m[1][2], matrix.m[1][1]);
        result.y = std::atan2(-matrix.m[2][0], sy);
        result.z = 0.0;
    }
    result.x *= radiansToDegrees;
    result.y *= radiansToDegrees;
    result.z *= radiansToDegrees;
    return result;
}

std::filesystem::path OutputPath() {
    std::array<wchar_t, 32768> buffer{};
    const DWORD length = GetModuleFileNameW(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
    return std::filesystem::path(std::wstring(buffer.data(), length)).parent_path() / L"nms-pose-result.txt";
}

std::string XrResultText(XrInstance instance, XrResult result) {
    char buffer[XR_MAX_RESULT_STRING_SIZE]{};
    if (instance != XR_NULL_HANDLE && XR_SUCCEEDED(xrResultToString(instance, result, buffer))) {
        return buffer;
    }
    return std::to_string(result);
}

bool HasExtension(const char* wanted) {
    uint32_t count = 0;
    if (XR_FAILED(xrEnumerateInstanceExtensionProperties(nullptr, 0, &count, nullptr))) {
        return false;
    }
    std::vector<XrExtensionProperties> properties(count, {XR_TYPE_EXTENSION_PROPERTIES});
    if (XR_FAILED(xrEnumerateInstanceExtensionProperties(nullptr, count, &count, properties.data()))) {
        return false;
    }
    return std::any_of(properties.begin(), properties.end(), [wanted](const XrExtensionProperties& property) {
        return std::string(property.extensionName) == wanted;
    });
}

bool IsPoseValid(const XrSpaceLocation& location) {
    constexpr XrSpaceLocationFlags wanted = XR_SPACE_LOCATION_POSITION_VALID_BIT | XR_SPACE_LOCATION_ORIENTATION_VALID_BIT;
    return (location.locationFlags & wanted) == wanted;
}

CorrectionSample ToSample(const Mat4& correction) {
    CorrectionSample sample{};
    sample.position = {correction.m[0][3], correction.m[1][3], correction.m[2][3]};
    sample.orientation = Normalize(MatrixToQuat(correction));
    return sample;
}

CorrectionSample Average(const std::vector<CorrectionSample>& samples) {
    CorrectionSample result{};
    if (samples.empty()) {
        return result;
    }
    Quat reference = samples.front().orientation;
    Quat sum{0.0, 0.0, 0.0, 0.0};
    for (const auto& sample : samples) {
        result.position.x += sample.position.x;
        result.position.y += sample.position.y;
        result.position.z += sample.position.z;
        const double sign = Dot(reference, sample.orientation) < 0.0 ? -1.0 : 1.0;
        sum.x += sign * sample.orientation.x;
        sum.y += sign * sample.orientation.y;
        sum.z += sign * sample.orientation.z;
        sum.w += sign * sample.orientation.w;
    }
    const double count = static_cast<double>(samples.size());
    result.position.x /= count;
    result.position.y /= count;
    result.position.z /= count;
    result.orientation = Normalize(sum);
    return result;
}

std::vector<CorrectionSample> CorrectionFromPoseAverages(
    const std::vector<CorrectionSample>& steamVrPoses,
    const std::vector<CorrectionSample>& openXrPoses) {
    if (steamVrPoses.empty() || openXrPoses.empty()) {
        return {};
    }
    const auto steamVrAverage = Average(steamVrPoses);
    const auto openXrAverage = Average(openXrPoses);
    const Mat4 steamVrMatrix = FromQuatAndPosition(steamVrAverage.orientation, steamVrAverage.position);
    const Mat4 openXrMatrix = FromQuatAndPosition(openXrAverage.orientation, openXrAverage.position);
    return {ToSample(Multiply(InverseRigid(openXrMatrix), steamVrMatrix))};
}

void WritePoseAverage(std::ostream& output, const char* name, const std::vector<CorrectionSample>& samples) {
    output << "\n[" << name << "]\n";
    output << "valid_samples=" << samples.size() << "\n";
    if (samples.empty()) {
        return;
    }
    const auto average = Average(samples);
    const auto euler = MatrixEulerDegrees(FromQuatAndPosition(average.orientation, average.position));
    output << std::fixed << std::setprecision(9);
    output << "relative_translation_m=" << average.position.x << ", " << average.position.y << ", " << average.position.z << "\n";
    output << "relative_euler_xyz_degrees=" << euler.x << ", " << euler.y << ", " << euler.z << "\n";
}

void WriteCorrection(std::ostream& output, const char* hand, const std::vector<CorrectionSample>& samples) {
    output << "\n[" << hand << "]\n";
    output << "valid_samples=" << samples.size() << "\n";
    if (samples.empty()) {
        output << "ERROR: no simultaneous valid pose samples\n";
        return;
    }
    const auto average = Average(samples);
    const auto matrix = FromQuatAndPosition(average.orientation, average.position);
    const auto euler = MatrixEulerDegrees(matrix);
    output << std::fixed << std::setprecision(9);
    output << "translation_m=" << average.position.x << ", " << average.position.y << ", " << average.position.z << "\n";
    output << "quaternion_xyzw=" << average.orientation.x << ", " << average.orientation.y << ", "
           << average.orientation.z << ", " << average.orientation.w << "\n";
    output << "euler_xyz_degrees=" << euler.x << ", " << euler.y << ", " << euler.z << "\n";
    output << "matrix_row_major=\n";
    for (int row = 0; row < 4; ++row) {
        output << "  ";
        for (int column = 0; column < 4; ++column) {
            output << std::setw(13) << matrix.m[row][column];
            if (column != 3) output << ", ";
        }
        output << "\n";
    }
}

void Pause() {
    std::cout << "\nAperte ENTER para fechar..." << std::endl;
    std::string ignored;
    std::getline(std::cin, ignored);
}

} // namespace

int main() {
    SetConsoleOutputCP(CP_UTF8);
    std::ostringstream report;
    report << "NMS SteamVR/VDXR pose probe v2 sequential\n";
    report << "Goal: correction M where OpenXR_grip_relative_to_HMD * M = SteamVR_raw_relative_to_HMD\n";

    std::cout << "Medidor de pose do No Man's Sky\n\n";
    std::cout << "Deixe o Virtual Desktop conectado, o SteamVR aberto e os dois controles acordados.\n";
    std::cout << "Apoie o headset e os dois controles numa mesa e NAO MOVA nada ate terminar.\n";
    std::cout << "O SteamVR vai fechar durante a troca para VDXR; isso e esperado.\n\n";
    std::cout << "Quando tudo estiver parado, aperte ENTER para comecar..." << std::endl;
    std::string beginInput;
    std::getline(std::cin, beginInput);

    vr::EVRInitError vrError = vr::VRInitError_None;
    vr::IVRSystem* vrSystem = vr::VR_Init(&vrError, vr::VRApplication_Background);
    if (vrError != vr::VRInitError_None || vrSystem == nullptr) {
        report << "OpenVR init failed: " << vr::VR_GetVRInitErrorAsEnglishDescription(vrError) << "\n";
        std::ofstream(OutputPath()) << report.str();
        std::cout << "ERRO: nao consegui conectar ao SteamVR. Abra o SteamVR primeiro.\n";
        Pause();
        return 1;
    }

    const auto leftIndex = vrSystem->GetTrackedDeviceIndexForControllerRole(vr::TrackedControllerRole_LeftHand);
    const auto rightIndex = vrSystem->GetTrackedDeviceIndexForControllerRole(vr::TrackedControllerRole_RightHand);
    report << "OpenVR left_index=" << leftIndex << " right_index=" << rightIndex << "\n";

    std::vector<CorrectionSample> steamVrLeftPoses;
    std::vector<CorrectionSample> steamVrRightPoses;
    const DWORD steamVrStart = GetTickCount();
    std::cout << "Etapa 1/2: medindo SteamVR...\n";
    while (GetTickCount() - steamVrStart < 10000 &&
           (steamVrLeftPoses.size() < 180 || steamVrRightPoses.size() < 180)) {
        std::array<vr::TrackedDevicePose_t, vr::k_unMaxTrackedDeviceCount> poses{};
        vrSystem->GetDeviceToAbsoluteTrackingPose(vr::TrackingUniverseStanding, 0.0f, poses.data(), static_cast<uint32_t>(poses.size()));
        if (poses[vr::k_unTrackedDeviceIndex_Hmd].bPoseIsValid) {
            const Mat4 headInverse = InverseRigid(FromOpenVr(poses[vr::k_unTrackedDeviceIndex_Hmd].mDeviceToAbsoluteTracking));
            const vr::TrackedDeviceIndex_t indices[2]{leftIndex, rightIndex};
            std::vector<CorrectionSample>* destinations[2]{&steamVrLeftPoses, &steamVrRightPoses};
            for (int hand = 0; hand < 2; ++hand) {
                const auto index = indices[hand];
                if (index != vr::k_unTrackedDeviceIndexInvalid && index < poses.size() && poses[index].bPoseIsValid &&
                    destinations[hand]->size() < 180) {
                    destinations[hand]->push_back(ToSample(Multiply(headInverse, FromOpenVr(poses[index].mDeviceToAbsoluteTracking))));
                }
            }
        }
        Sleep(10);
    }
    WritePoseAverage(report, "steamvr_left_relative", steamVrLeftPoses);
    WritePoseAverage(report, "steamvr_right_relative", steamVrRightPoses);
    vr::VR_Shutdown();
    vrSystem = nullptr;

    if (steamVrLeftPoses.empty() || steamVrRightPoses.empty()) {
        report << "SteamVR stage failed: missing valid headset/controller poses\n";
        std::ofstream(OutputPath()) << report.str();
        std::cout << "ERRO: o SteamVR nao enxergou o headset e os dois controles.\n";
        Pause();
        return 2;
    }
    std::cout << "Etapa 1 concluida. Continue sem mover nada; trocando para VDXR...\n";

    if (!HasExtension(XR_MND_HEADLESS_EXTENSION_NAME)) {
        report << "OpenXR error: XR_MND_headless is unavailable\n";
        std::ofstream(OutputPath()) << report.str();
        std::cout << "ERRO: o runtime OpenXR ativo nao oferece o modo de medicao necessario.\n";
        Pause();
        return 3;
    }

    const char* extensions[] = {XR_MND_HEADLESS_EXTENSION_NAME};
    XrInstanceCreateInfo instanceInfo{XR_TYPE_INSTANCE_CREATE_INFO};
    std::strncpy(instanceInfo.applicationInfo.applicationName, "NMS Pose Probe", XR_MAX_APPLICATION_NAME_SIZE - 1);
    instanceInfo.applicationInfo.applicationVersion = 1;
    std::strncpy(instanceInfo.applicationInfo.engineName, "OpenComposite NMS", XR_MAX_ENGINE_NAME_SIZE - 1);
    instanceInfo.applicationInfo.engineVersion = 1;
    instanceInfo.applicationInfo.apiVersion = XR_MAKE_VERSION(1, 0, 0);
    instanceInfo.enabledExtensionCount = 1;
    instanceInfo.enabledExtensionNames = extensions;

    XrInstance instance = XR_NULL_HANDLE;
    XrResult xrResult = xrCreateInstance(&instanceInfo, &instance);
    if (XR_FAILED(xrResult)) {
        report << "OpenXR instance failed: " << xrResult << "\n";
        std::ofstream(OutputPath()) << report.str();
        std::cout << "ERRO: nao consegui iniciar o VDXR/OpenXR (codigo " << xrResult << ").\n";
        Pause();
        return 4;
    }

    XrSystemGetInfo systemInfo{XR_TYPE_SYSTEM_GET_INFO};
    systemInfo.formFactor = XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY;
    XrSystemId systemId = XR_NULL_SYSTEM_ID;
    xrResult = xrGetSystem(instance, &systemInfo, &systemId);
    if (XR_FAILED(xrResult)) {
        report << "xrGetSystem failed: " << XrResultText(instance, xrResult) << "\n";
        std::ofstream(OutputPath()) << report.str();
        std::cout << "ERRO: o VDXR nao encontrou o headset conectado.\n";
        xrDestroyInstance(instance);
        Pause();
        return 5;
    }

    XrSessionCreateInfo sessionInfo{XR_TYPE_SESSION_CREATE_INFO};
    sessionInfo.systemId = systemId;
    XrSession session = XR_NULL_HANDLE;
    xrResult = xrCreateSession(instance, &sessionInfo, &session);
    if (XR_FAILED(xrResult)) {
        report << "xrCreateSession failed: " << XrResultText(instance, xrResult) << "\n";
        std::ofstream(OutputPath()) << report.str();
        std::cout << "ERRO: nao consegui criar a sessao de medicao do VDXR.\n";
        xrDestroyInstance(instance);
        Pause();
        return 6;
    }

    XrPath handPaths[2]{};
    xrStringToPath(instance, "/user/hand/left", &handPaths[0]);
    xrStringToPath(instance, "/user/hand/right", &handPaths[1]);

    XrActionSetCreateInfo actionSetInfo{XR_TYPE_ACTION_SET_CREATE_INFO};
    std::strncpy(actionSetInfo.actionSetName, "pose_measurement", XR_MAX_ACTION_SET_NAME_SIZE - 1);
    std::strncpy(actionSetInfo.localizedActionSetName, "Pose measurement", XR_MAX_LOCALIZED_ACTION_SET_NAME_SIZE - 1);
    actionSetInfo.priority = 0;
    XrActionSet actionSet = XR_NULL_HANDLE;
    xrCreateActionSet(instance, &actionSetInfo, &actionSet);

    XrActionCreateInfo actionInfo{XR_TYPE_ACTION_CREATE_INFO};
    actionInfo.actionType = XR_ACTION_TYPE_POSE_INPUT;
    std::strncpy(actionInfo.actionName, "hand_grip_pose", XR_MAX_ACTION_NAME_SIZE - 1);
    std::strncpy(actionInfo.localizedActionName, "Hand grip pose", XR_MAX_LOCALIZED_ACTION_NAME_SIZE - 1);
    actionInfo.countSubactionPaths = 2;
    actionInfo.subactionPaths = handPaths;
    XrAction poseAction = XR_NULL_HANDLE;
    xrCreateAction(actionSet, &actionInfo, &poseAction);

    XrPath interactionProfile = XR_NULL_PATH;
    XrPath leftGrip = XR_NULL_PATH;
    XrPath rightGrip = XR_NULL_PATH;
    xrStringToPath(instance, "/interaction_profiles/oculus/touch_controller", &interactionProfile);
    xrStringToPath(instance, "/user/hand/left/input/grip/pose", &leftGrip);
    xrStringToPath(instance, "/user/hand/right/input/grip/pose", &rightGrip);
    std::array<XrActionSuggestedBinding, 2> bindings{{{poseAction, leftGrip}, {poseAction, rightGrip}}};
    XrInteractionProfileSuggestedBinding suggested{XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING};
    suggested.interactionProfile = interactionProfile;
    suggested.countSuggestedBindings = static_cast<uint32_t>(bindings.size());
    suggested.suggestedBindings = bindings.data();
    xrSuggestInteractionProfileBindings(instance, &suggested);

    XrSessionActionSetsAttachInfo attachInfo{XR_TYPE_SESSION_ACTION_SETS_ATTACH_INFO};
    attachInfo.countActionSets = 1;
    attachInfo.actionSets = &actionSet;
    xrAttachSessionActionSets(session, &attachInfo);

    XrPosef identityPose{{0, 0, 0, 1}, {0, 0, 0}};
    XrReferenceSpaceCreateInfo localInfo{XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
    localInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_LOCAL;
    localInfo.poseInReferenceSpace = identityPose;
    XrSpace localSpace = XR_NULL_HANDLE;
    xrCreateReferenceSpace(session, &localInfo, &localSpace);

    XrReferenceSpaceCreateInfo viewInfo{XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
    viewInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_VIEW;
    viewInfo.poseInReferenceSpace = identityPose;
    XrSpace viewSpace = XR_NULL_HANDLE;
    xrCreateReferenceSpace(session, &viewInfo, &viewSpace);

    XrSpace handSpaces[2]{XR_NULL_HANDLE, XR_NULL_HANDLE};
    for (int hand = 0; hand < 2; ++hand) {
        XrActionSpaceCreateInfo handSpaceInfo{XR_TYPE_ACTION_SPACE_CREATE_INFO};
        handSpaceInfo.action = poseAction;
        handSpaceInfo.subactionPath = handPaths[hand];
        handSpaceInfo.poseInActionSpace = identityPose;
        xrCreateActionSpace(session, &handSpaceInfo, &handSpaces[hand]);
    }

    bool sessionBegun = false;
    bool exitRequested = false;
    std::vector<CorrectionSample> openXrLeftPoses;
    std::vector<CorrectionSample> openXrRightPoses;
    const DWORD startTime = GetTickCount();

    std::cout << "Etapa 2/2: medindo VDXR...\n";
    while (!exitRequested && GetTickCount() - startTime < 20000 &&
           (openXrLeftPoses.size() < 180 || openXrRightPoses.size() < 180)) {
        XrEventDataBuffer event{XR_TYPE_EVENT_DATA_BUFFER};
        while (xrPollEvent(instance, &event) == XR_SUCCESS) {
            if (event.type == XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED) {
                const auto* stateEvent = reinterpret_cast<const XrEventDataSessionStateChanged*>(&event);
                if (stateEvent->state == XR_SESSION_STATE_READY && !sessionBegun) {
                    XrSessionBeginInfo beginInfo{XR_TYPE_SESSION_BEGIN_INFO};
                    beginInfo.primaryViewConfigurationType = XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;
                    xrResult = xrBeginSession(session, &beginInfo);
                    sessionBegun = XR_SUCCEEDED(xrResult);
                    report << "xrBeginSession=" << XrResultText(instance, xrResult) << "\n";
                } else if (stateEvent->state == XR_SESSION_STATE_STOPPING && sessionBegun) {
                    xrEndSession(session);
                    sessionBegun = false;
                } else if (stateEvent->state == XR_SESSION_STATE_EXITING || stateEvent->state == XR_SESSION_STATE_LOSS_PENDING) {
                    exitRequested = true;
                }
            }
            event = {XR_TYPE_EVENT_DATA_BUFFER};
        }

        if (!sessionBegun) {
            Sleep(20);
            continue;
        }

        XrFrameWaitInfo waitInfo{XR_TYPE_FRAME_WAIT_INFO};
        XrFrameState frameState{XR_TYPE_FRAME_STATE};
        xrResult = xrWaitFrame(session, &waitInfo, &frameState);
        if (XR_FAILED(xrResult)) {
            report << "xrWaitFrame failed: " << XrResultText(instance, xrResult) << "\n";
            break;
        }
        XrFrameBeginInfo frameBeginInfo{XR_TYPE_FRAME_BEGIN_INFO};
        xrBeginFrame(session, &frameBeginInfo);

        XrActiveActionSet activeActionSet{actionSet, XR_NULL_PATH};
        XrActionsSyncInfo syncInfo{XR_TYPE_ACTIONS_SYNC_INFO};
        syncInfo.countActiveActionSets = 1;
        syncInfo.activeActionSets = &activeActionSet;
        xrSyncActions(session, &syncInfo);

        XrSpaceLocation headLocation{XR_TYPE_SPACE_LOCATION};
        XrSpaceLocation handLocations[2]{{XR_TYPE_SPACE_LOCATION}, {XR_TYPE_SPACE_LOCATION}};
        xrLocateSpace(viewSpace, localSpace, frameState.predictedDisplayTime, &headLocation);
        xrLocateSpace(handSpaces[0], localSpace, frameState.predictedDisplayTime, &handLocations[0]);
        xrLocateSpace(handSpaces[1], localSpace, frameState.predictedDisplayTime, &handLocations[1]);

        if (IsPoseValid(headLocation)) {
            const Mat4 xrHead = FromOpenXr(headLocation.pose);
            const Mat4 xrHeadInverse = InverseRigid(xrHead);
            std::vector<CorrectionSample>* samples[2]{&openXrLeftPoses, &openXrRightPoses};
            for (int hand = 0; hand < 2; ++hand) {
                if (IsPoseValid(handLocations[hand])) {
                    const Mat4 xrRelative = Multiply(xrHeadInverse, FromOpenXr(handLocations[hand].pose));
                    if (samples[hand]->size() < 180) {
                        samples[hand]->push_back(ToSample(xrRelative));
                    }
                }
            }
        }

        XrFrameEndInfo endInfo{XR_TYPE_FRAME_END_INFO};
        endInfo.displayTime = frameState.predictedDisplayTime;
        endInfo.environmentBlendMode = XR_ENVIRONMENT_BLEND_MODE_OPAQUE;
        endInfo.layerCount = 0;
        endInfo.layers = nullptr;
        xrEndFrame(session, &endInfo);
    }

    WritePoseAverage(report, "vdxr_left_relative", openXrLeftPoses);
    WritePoseAverage(report, "vdxr_right_relative", openXrRightPoses);
    const auto leftCorrection = CorrectionFromPoseAverages(steamVrLeftPoses, openXrLeftPoses);
    const auto rightCorrection = CorrectionFromPoseAverages(steamVrRightPoses, openXrRightPoses);
    WriteCorrection(report, "left_correction", leftCorrection);
    WriteCorrection(report, "right_correction", rightCorrection);

    XrInstanceProperties runtimeProperties{XR_TYPE_INSTANCE_PROPERTIES};
    if (XR_SUCCEEDED(xrGetInstanceProperties(instance, &runtimeProperties))) {
        report << "\nOpenXR runtime=" << runtimeProperties.runtimeName << " "
               << XR_VERSION_MAJOR(runtimeProperties.runtimeVersion) << "."
               << XR_VERSION_MINOR(runtimeProperties.runtimeVersion) << "."
               << XR_VERSION_PATCH(runtimeProperties.runtimeVersion) << "\n";
    }

    const auto outputPath = OutputPath();
    std::ofstream output(outputPath);
    output << report.str();
    output.close();

    for (auto& handSpace : handSpaces) if (handSpace != XR_NULL_HANDLE) xrDestroySpace(handSpace);
    if (viewSpace != XR_NULL_HANDLE) xrDestroySpace(viewSpace);
    if (localSpace != XR_NULL_HANDLE) xrDestroySpace(localSpace);
    if (poseAction != XR_NULL_HANDLE) xrDestroyAction(poseAction);
    if (actionSet != XR_NULL_HANDLE) xrDestroyActionSet(actionSet);
    if (sessionBegun) xrEndSession(session);
    xrDestroySession(session);
    xrDestroyInstance(instance);
    std::cout << "\nPronto. Resultado salvo em:\n" << outputPath.string() << "\n";
    std::cout << "Amostras VDXR: esquerda=" << openXrLeftPoses.size() << ", direita=" << openXrRightPoses.size() << "\n";
    if (leftCorrection.empty() || rightCorrection.empty()) {
        std::cout << "Nao consegui enxergar todas as poses; confira o arquivo de resultado.\n";
    } else {
        std::cout << "Medicao concluida com sucesso.\n";
    }
    Pause();
    return (leftCorrection.empty() || rightCorrection.empty()) ? 7 : 0;
}
