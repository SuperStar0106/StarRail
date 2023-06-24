[English](./README.md) | 简体中文


# What is Starward?

**Starward** 出自星穹铁道开服前的宣传语———愿此行，终抵群星 (May This Journey Lead Us **Starward**)，虽然这不是一个正确的英文单词，但是很适合拿来用作应用名。Starward 是一个米家游戏启动器，支持米哈游旗下的所有桌面端游戏，本项目的目标是完全替代官方的启动器，并在此基础上加入一些拓展功能。

除了游戏的下载安装之外，还包括以下功能：

- 记录游戏时间
- 切换游戏账号
- 浏览游戏截图
- 保存抽卡记录

更多功能正在计划中。。。

> Starward 不会加入需要开发者持续更新游戏数据和资源的功能，比如给抽卡记录加上物品图片。


## 下载

> 仅支持 Windows 10 1809 (17763) 及以上的版本

你可在 [Release](https://github.com/Scighost/Starward/releases) 页面下载最新发布的版本，应用使用增量更新的方式，既简单又便捷。


## 本地化

[![zh-CN translation](https://img.shields.io/badge/dynamic/json?color=blue&label=zh-CN&style=flat&logo=crowdin&query=%24.progress[?(@.data.languageId==%27zh-CN%27)].data.translationProgress&url=https%3A%2F%2Fbadges.awesome-crowdin.com%2Fstats-15878835-595799.json)](https://crowdin.com/project/starward/zh-CN)
[![en-US translation](https://img.shields.io/badge/dynamic/json?color=blue&label=en-US&style=flat&logo=crowdin&query=%24.progress[?(@.data.languageId==%27en-US%27)].data.translationProgress&url=https%3A%2F%2Fbadges.awesome-crowdin.com%2Fstats-15878835-595799.json)](https://crowdin.com/project/starward/en-US)
[![vi-VN translation](https://img.shields.io/badge/dynamic/json?color=blue&label=vi-VN&style=flat&logo=crowdin&query=%24.progress[?(@.data.languageId==%27vi%27)].data.translationProgress&url=https%3A%2F%2Fbadges.awesome-crowdin.com%2Fstats-15878835-595799.json)](https://crowdin.com/project/starward/en-US)

Starward 使用 [Crowdin](https://crowdin.com/project/starward) 进行本地化工作，提供机翻的英文文本作为原文。你可以帮助我们翻译和校对本地语言，我们期待有更多的人加入。如果你想增加一个新的翻译语言，请在 issue 中提出请求。


## 开发

在本地编译应用，你需要安装 Visual Studio 2022 并选择以下负载：

- .NET 桌面开发
- 使用 C++ 的桌面开发
- 通用 Windows 平台开发


## 致谢

首先我要特别感谢 [neon-nyan](https://github.com/neon-nyan)，本项目的灵感正是来源于他的项目 [Collapse](https://github.com/neon-nyan/Collapse)，Starward 不仅使用了他制作的部分素材，还在界面设计上模仿到了极致。Collapse 的代码让我学到了很多知识，有此珠玉在前，我的开发过程顺利了很多。

其次，感谢 CloudFlare 提供的免费 CDN。

<img alt="cloudflare" width="300px" src="https://user-images.githubusercontent.com/61003590/246605903-f19b5ae7-33f8-41ac-8130-6d0069fde27a.png" />

本项目中使用到的第三方库包括：

- [Dapper](https://github.com/DapperLib/Dapper)
- [GitHub Markdown CSS](https://github.com/sindresorhus/github-markdown-css)
- [HDiffPatch](https://github.com/sisong/HDiffPatch)
- [Markdig](https://github.com/xoofx/markdig)
- [MiniExcel](https://github.com/mini-software/MiniExcel)
- [Serilog](https://github.com/serilog/serilog)
- [SevenZipExtractor](https://github.com/adoconnection/SevenZipExtractor)
- [Vanara PInvoke](https://github.com/dahall/Vanara)
- [WindowsAppSDK](https://github.com/microsoft/WindowsAppSDK)
- [WindowsCommunityToolkit](https://github.com/CommunityToolkit/WindowsCommunityToolkit)


## 截图

![screenshot](https://user-images.githubusercontent.com/61003590/246605666-56adfd7f-0e5f-471b-beeb-f6ec4430f89b.png)
