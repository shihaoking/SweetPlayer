## 新增需求

### 需求：HDR 格式检测
系统应分析视频文件元数据，通过检查色彩传输特性和母版元数据来检测 HDR10、HDR10+ 和 HLG 高动态范围格式。

#### 场景：视频文件使用 PQ 传输函数和静态元数据
- **WHEN** 视频文件的流信息指示 SMPTE ST 2084 传输且包含母版显示元数据
- **THEN** 系统将其识别为 HDR10 并相应标记

#### 场景：视频文件使用 HLG 传输
- **WHEN** 视频文件指示 ARIB STD-B67 (HLG) 传输函数
- **THEN** 系统将其识别为 HLG HDR 内容

### 需求：Dolby Vision 检测
系统应通过检查视频流中的 HEVC 配置记录和 RPU（参考处理单元）NAL 单元来检测 Dolby Vision 内容。

#### 场景：视频包含 Dolby Vision Profile 7
- **WHEN** 视频文件包含带 Dolby Vision 增强层的 HEVC（双层，Profile 7）
- **THEN** 系统将其识别为 Dolby Vision 内容

#### 场景：视频包含 Dolby Vision Profile 5/8
- **WHEN** 视频文件包含单层 Dolby Vision（Profile 5 或 8，含 RPU）
- **THEN** 系统将其识别为 Dolby Vision 内容

### 需求：Dolby Atmos 检测
系统应通过检查音频流编解码器信息中的 E-AC-3 JOC（联合对象编码）或 TrueHD Atmos 元数据来检测 Dolby Atmos 音频。

#### 场景：视频含带 JOC 的 E-AC-3
- **WHEN** 视频文件包含带 Joint Object Coding 标志的 E-AC-3 音轨
- **THEN** 系统将其识别为 Dolby Atmos 音频

#### 场景：视频含带 Atmos 的 TrueHD
- **WHEN** 视频文件包含带 Atmos 元数据的 TrueHD 音轨
- **THEN** 系统将其识别为 Dolby Atmos 音频

### 需求：自动激活 Windows HDR
系统应在播放 HDR 或 Dolby Vision 视频时自动启用 Windows 系统 HDR 显示模式，并在播放结束时恢复之前的 HDR 状态。

#### 场景：在支持 HDR 的显示器上播放 HDR 视频（HDR 已关闭）
- **WHEN** 用户播放 HDR10 视频且 Windows HDR 设置当前为禁用且显示器支持 HDR
- **THEN** 系统在开始播放前启用 Windows HDR 模式

#### 场景：HDR 视频播放结束
- **WHEN** HDR 视频播放完毕或用户停止播放且 HDR 是自动启用的
- **THEN** 系统将 Windows HDR 设置恢复到先前状态（禁用）

#### 场景：显示器不支持 HDR
- **WHEN** 用户播放 HDR 视频但连接的显示器不支持 HDR
- **THEN** 系统正常播放视频，不尝试启用 HDR，并显示通知提示 HDR 不可用
