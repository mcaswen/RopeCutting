# Unity 开发规范 v1.1

## 重点关注
- [策划、美术、程序] 目录规范、命名规范、Git协作规范、场景/Prefab/SO规范、协作规范，不随意修改他人模块
- [程序] 脚本/变量命名、代码原则、注释规范、注意风险点；可使用AI辅助但必须Code Review
- [策划、美术] 非必要不修改除 Art/Audio/Scenes/SO/Docs 外文件夹

## 目录规范
- Assets/Art/ (Animations, Animation Controllers, Materials, Models, Sprites, VFX)
- Assets/Audio/
- Assets/Scenes/
- Assets/Scripts/ (Core/, Gameplay/, UI/, Systems/, Tools/, Editor/)
- Assets/SO/ (ScriptableObject)
- Assets/Prefabs/
- Assets/Docs/
- Assets/ThirdParty/
- Assets/Settings/
- Assets/Shader/
- 场景放 Scenes，脚本放 Scripts，SO 放 SO，第三方插件放 ThirdParty
- 临时资源用完删除或归档

## 命名规范
- 脚本名 = 类名（MonoBehaviour），帕斯卡命名法
- 公有字段帕斯卡，私有字段 _camelCase
- 属性帕斯卡，常量帕斯卡
- 资源前缀：Pfb_, Clip_, AniCtr_, SO_, Scene_, 其他用后缀名首字母大写如 Png_, Fbx_
- 格式："前缀_模块_对象"

## 代码原则
- 单一职责，避免上帝类
- 核心/表现/数据分离
- 默认 private，Inspector 暴露用 [SerializeField] private
- 方法名清晰表达行为，一个方法只做一件事
- Update() 只保留逐帧必要逻辑，缓存引用避免每帧查找
- 命名空间 = 文件所属文件夹相对路径
- UTF-8 编码

## 注释规范
- 删除 AI 遗留的无意义注释
- 注释写"为什么这样做"而非重复代码
- 复杂类/公共方法用 XML <summary>
- 私有方法用 // 注释，过长也用 <summary>

## 架构分层
- 表现层：UI、动画、特效、音效、镜头
- 逻辑层：玩法规则、战斗流程、技能结算
- 数据层：静态配置、ScriptableObject、存档
- UI 不直接修改核心数据，模块间优先接口/事件通信

## 场景/Prefab/SO
- 不直接修改他人场景，需编辑则复制改名
- 公共管理对象放 Managers/Bootstrap 节点
- 可复用对象做 Prefab
- SO 用于静态配置不承载运行时状态

## Git与协作
- 半天 check 飞书同步信息
- DDL 无法完成提前同步
- 不把测试代码/调试输出留正式版本
- 修改公共逻辑说明影响范围
- 所有修改在 Git 仓库统一进行

## 主要风险点
1. AI 代码不作检查直接使用
2. 直接修改他人 Scene
3. Git 冲突不解决滞留项目中
