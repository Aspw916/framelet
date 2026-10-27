# 第三方组件

帧刻使用独立的 FFmpeg 命令行进程捕获屏幕和编码 GIF，不向网络服务发送画面。

本源码仓库不包含 FFmpeg 二进制。构建者需自行提供该组件；下列 `licenses/` 文件由构建脚本写入生成的便携目录中。

本地打包来源：Gyan.dev 的 FFmpeg 8.1 full_build Windows x64 静态构建，通过本机既有 WinGet 安装取得，程序未经修改。

- FFmpeg 项目：https://ffmpeg.org/
- Windows 构建提供者：https://www.gyan.dev/ffmpeg/builds/
- 对应上游源码：https://ffmpeg.org/releases/ffmpeg-8.1.tar.xz
- Windows 构建说明与配置来源：https://github.com/GyanD/codexffmpeg
- 此构建的完整版本和编译参数：`licenses/FFmpeg-build.txt`
- 随组件附带的 GNU GPL v3 文本：`licenses/FFmpeg-LICENSE.txt`

该 FFmpeg 构建启用了 GPL v3 组件（例如 libx264），相应组件按各自许可证提供。FFmpeg 版权属于 FFmpeg 开发者和相关组件作者。本工具不将 FFmpeg 作为私有自有代码声明。对外再次分发该编码组件前，应按其许可证提供相应的完整对应源码及所需构建材料。

.NET Framework、Windows API 与系统字体由 Windows / Microsoft 提供，不包含在本工具的编码组件中。
