# Changelog

## [v1.0.0-beta.4](https://github.com/devlooped/stunts/tree/v1.0.0-beta.4) (2026-10-16)

[Full Changelog](https://github.com/devlooped/stunts/compare/v1.0.0-beta.3...v1.0.0-beta.4)

:sparkles: Implemented enhancements:

- Allow StuntBuilder to accept behavior factories for per-instance stateful behaviors [\#276](https://github.com/devlooped/stunts/issues/276)
- Add a synchronized behavior [\#271](https://github.com/devlooped/stunts/issues/271)
- Add property change notification behaviors [\#270](https://github.com/devlooped/stunts/issues/270)
- Add an exception-mapping behavior [\#269](https://github.com/devlooped/stunts/issues/269)
- Add a guard behavior for validation and authorization [\#268](https://github.com/devlooped/stunts/issues/268)
- Add a Polly resilience behavior [\#266](https://github.com/devlooped/stunts/issues/266)
- Add an observation behavior for logging, duration, and tracing [\#265](https://github.com/devlooped/stunts/issues/265)
- Add a target-forwarding behavior, including a lazy target [\#264](https://github.com/devlooped/stunts/issues/264)
- Add an async-aware Proceed extension for behaviors [\#263](https://github.com/devlooped/stunts/issues/263)
- Support for default interface implementations [\#113](https://github.com/devlooped/stunts/issues/113)
- Add a guard behavior for validation and authorization [\#291](https://github.com/devlooped/stunts/pull/291) (@kzu)
- Add an AOT-safe data annotation behavior [\#290](https://github.com/devlooped/stunts/pull/290) (@kzu)
- Speed up compile-time stunt generation [\#287](https://github.com/devlooped/stunts/pull/287) (@kzu)
- Add an exception-mapping behavior [\#285](https://github.com/devlooped/stunts/pull/285) (@kzu)
- Add a synchronized behavior that holds through async completion [\#284](https://github.com/devlooped/stunts/pull/284) (@kzu)
- Add NotifyPropertyChangeBehavior [\#282](https://github.com/devlooped/stunts/pull/282) (@kzu)
- Add AddBehavior\<TBehavior\> overload [\#281](https://github.com/devlooped/stunts/pull/281) (@kzu)
- Add an observability behavior for logging, duration, and tracing [\#280](https://github.com/devlooped/stunts/pull/280) (@kzu)
- Add a target-forwarding behavior, including a lazy target. [\#279](https://github.com/devlooped/stunts/pull/279) (@kzu)
- Allow StuntBuilder to accept behavior factories and clone ICloneable behaviors [\#277](https://github.com/devlooped/stunts/pull/277) (@kzu)
- Add an async-aware Proceed extension for behaviors. [\#273](https://github.com/devlooped/stunts/pull/273) (@kzu)
- Name the non-array default argument [\#262](https://github.com/devlooped/stunts/pull/262) (@kzu)
- Generate closed Native AOT stunts from call sites [\#257](https://github.com/devlooped/stunts/pull/257) (@kzu)
- Generate comparison-package acceptance coverage [\#256](https://github.com/devlooped/stunts/pull/256) (@kzu)
- Support generic proxy templates and compatible signatures [\#254](https://github.com/devlooped/stunts/pull/254) (@kzu)
- Constrain behavior extension methods to IStunt [\#253](https://github.com/devlooped/stunts/pull/253) (@kzu)
- Add StuntReference, Stunt.For/Get and StuntBuilder [\#252](https://github.com/devlooped/stunts/pull/252) (@kzu)
- Use ambient behavior pipeline factory in tests [\#249](https://github.com/devlooped/stunts/pull/249) (@kzu)
- Validate null value-type results [\#248](https://github.com/devlooped/stunts/pull/248) (@kzu)
- Support Stunt.Of\<SomeDelegate\>\(\) so a delegate can be the pipeline tail [\#246](https://github.com/devlooped/stunts/pull/246) (@kzu)
- Rename StaticStuntFactory to CompiledStuntFactory [\#245](https://github.com/devlooped/stunts/pull/245) (@kzu)
- Copy custom attributes onto generated stunts [\#244](https://github.com/devlooped/stunts/pull/244) (@kzu)
- Error when unsafe stunt signatures lack compile-time stunts or unsafe blocks [\#242](https://github.com/devlooped/stunts/pull/242) (@kzu)
- Emit SignatureRef types at design time [\#241](https://github.com/devlooped/stunts/pull/241) (@kzu)
- Rename StaticProxy to CompiledProxy [\#240](https://github.com/devlooped/stunts/pull/240) (@kzu)
- Generate stunts for ref struct, pointer, and ref readonly signatures [\#239](https://github.com/devlooped/stunts/pull/239) (@kzu)
- Proxy internal, private protected, and private nested types [\#238](https://github.com/devlooped/stunts/pull/238) (@kzu)
- Intercept default interface members [\#236](https://github.com/devlooped/stunts/pull/236) (@kzu)
- Emit a partial record when the stunt base type is a record [\#234](https://github.com/devlooped/stunts/pull/234) (@kzu)

:bug: Fixed bugs:

- Fix concurrent and simple-name dependency resolution [\#255](https://github.com/devlooped/stunts/pull/255) (@kzu)
- Preserve narrowed accessor accessibility [\#237](https://github.com/devlooped/stunts/pull/237) (@kzu)
- Close generic methods before creating the invocation [\#235](https://github.com/devlooped/stunts/pull/235) (@kzu)

:hammer: Other:

- Bump open dependency updates together [\#295](https://github.com/devlooped/stunts/issues/295)

:twisted_rightwards_arrows: Merged:

- Bump files with dotnet-file sync [\#302](https://github.com/devlooped/stunts/pull/302) (@kzu)
- Bump open dependency updates together [\#296](https://github.com/devlooped/stunts/pull/296) (@Copilot)
- Format generated stunts with explicit whitespace trivia. [\#288](https://github.com/devlooped/stunts/pull/288) (@kzu)
- Run exception mapping tests without the scenario compiler [\#286](https://github.com/devlooped/stunts/pull/286) (@kzu)
- Make the synchronous Proceed callback test deterministic [\#278](https://github.com/devlooped/stunts/pull/278) (@kzu)
- Run the OS build matrix only for publish and release [\#261](https://github.com/devlooped/stunts/pull/261) (@kzu)
- Document agent skill tracker, labels, and domain docs [\#260](https://github.com/devlooped/stunts/pull/260) (@kzu)
- Rename leftover stunt shape terms to definitions [\#259](https://github.com/devlooped/stunts/pull/259) (@kzu)
- Improve pipeline factory docs [\#251](https://github.com/devlooped/stunts/pull/251) (@kzu)
- Showcase Stunts features with progressive C\# examples [\#250](https://github.com/devlooped/stunts/pull/250) (@kzu)
- Name generated implementation lambda parameters clearly [\#247](https://github.com/devlooped/stunts/pull/247) (@kzu)
- Bump files with dotnet-file sync [\#243](https://github.com/devlooped/stunts/pull/243) (@kzu)
- Emit scenario generated sources to obj for inspection [\#233](https://github.com/devlooped/stunts/pull/233) (@kzu)
- Showcase support for default interface implementation [\#232](https://github.com/devlooped/stunts/pull/232) (@kzu)

## [v1.0.0-beta.3](https://github.com/devlooped/stunts/tree/v1.0.0-beta.3) (2026-09-29)

[Full Changelog](https://github.com/devlooped/stunts/compare/v1.0.0-beta.2...v1.0.0-beta.3)

:bug: Fixed bugs:

- Fix AmbiguousMatchException for default ValueTask\<T\> of reference types [\#231](https://github.com/devlooped/stunts/pull/231) (@kzu)

## [v1.0.0-beta.2](https://github.com/devlooped/stunts/tree/v1.0.0-beta.2) (2026-09-28)

[Full Changelog](https://github.com/devlooped/stunts/compare/v1.0.0-beta.1...v1.0.0-beta.2)

:twisted_rightwards_arrows: Merged:

- Stop inlining expanded content in package readmes [\#228](https://github.com/devlooped/stunts/pull/228) (@kzu)

## [v1.0.0-beta.1](https://github.com/devlooped/stunts/tree/v1.0.0-beta.1) (2026-09-28)

[Full Changelog](https://github.com/devlooped/stunts/compare/v1.0.0-beta...v1.0.0-beta.1)

:sparkles: Implemented enhancements:

- Remove dependency on code fixers and refactorings from Roslyn internals [\#132](https://github.com/devlooped/stunts/issues/132)
- Avatar source generation processors should get updated compilation from context [\#131](https://github.com/devlooped/stunts/issues/131)
- Rename callBase to implementation [\#108](https://github.com/devlooped/stunts/issues/108)
- When emitting generated files, whitespace/newlines are missing [\#98](https://github.com/devlooped/stunts/issues/98)
- Make Stunts.DynamicProxy an explicit opt-in package [\#218](https://github.com/devlooped/stunts/pull/218) (@kzu)
- Apply OSMF licensing and the original Stunts icon [\#214](https://github.com/devlooped/stunts/pull/214) (@kzu)
- Restore the Stunts name [\#211](https://github.com/devlooped/stunts/pull/211) (@kzu)
- Generate proxies without Roslyn internals. [\#204](https://github.com/devlooped/stunts/pull/204) (@kzu)
- Improve design of syntax processors and introduce a driver class [\#139](https://github.com/devlooped/stunts/pull/139) (@kzu)
- Use local function in pipleline invocation to avoid delegate allocation [\#101](https://github.com/devlooped/stunts/pull/101) (@atifaziz)

:bug: Fixed bugs:

- Add analysis error if target type has no public or protected constructor  [\#129](https://github.com/devlooped/stunts/issues/129)
- Source generators are reported as not supported under 16.10 preview [\#125](https://github.com/devlooped/stunts/issues/125)
- Error when packing because of mismatch of System.Runtime.CompilerServices.Unsafe [\#117](https://github.com/devlooped/stunts/issues/117)
- Nullable warnings when out/ref argument is nullable [\#99](https://github.com/devlooped/stunts/issues/99)
- Exclude Microsoft.CSharp from the static proxy tools [\#226](https://github.com/devlooped/stunts/pull/226) (@kzu)
- 👀 Fail if no visible constructors available [\#130](https://github.com/devlooped/stunts/pull/130) (@kzu)

:hammer: Other:

- Turn on build-time failures for .editorconfig violations [\#60](https://github.com/devlooped/stunts/issues/60)

:twisted_rightwards_arrows: Merged:

- Drop the trailing newline in src/nuget.config [\#223](https://github.com/devlooped/stunts/pull/223) (@kzu)
- Point the Ipsum comment at the devlooped/catbag source [\#222](https://github.com/devlooped/stunts/pull/222) (@kzu)
- Retarget unit tests at net10.0 [\#221](https://github.com/devlooped/stunts/pull/221) (@kzu)
- Drop the duplicate Solution Items folder from the solution [\#220](https://github.com/devlooped/stunts/pull/220) (@kzu)
- Replace the alien mark with the purple helmet [\#217](https://github.com/devlooped/stunts/pull/217) (@kzu)
- Clear NuGet, Roslyn, and xUnit warnings [\#212](https://github.com/devlooped/stunts/pull/212) (@kzu)
- Bump to latest version of 3.10 pre-release [\#149](https://github.com/devlooped/stunts/pull/149) (@kzu)
- Bump files with dotnet-file sync [\#140](https://github.com/devlooped/stunts/pull/140) (@kzu)
- Update to latest NuGet protocol package [\#135](https://github.com/devlooped/stunts/pull/135) (@kzu)
- 🔄 dotnet-file sync [\#120](https://github.com/devlooped/stunts/pull/120) (@kzu)
- 🔄 dotnet-file sync [\#114](https://github.com/devlooped/stunts/pull/114) (@kzu)
- Rename callBase to implementation [\#109](https://github.com/devlooped/stunts/pull/109) (@kzu)
- 🔄 dotnet-file sync [\#107](https://github.com/devlooped/stunts/pull/107) (@kzu)
- Miscelaneous minor improvements [\#100](https://github.com/devlooped/stunts/pull/100) (@kzu)
- 🖆 Apply kzu/oss template via dotnet-file [\#96](https://github.com/devlooped/stunts/pull/96) (@kzu)

## [v1.0.0-beta](https://github.com/devlooped/stunts/tree/v1.0.0-beta) (2021-01-19)

[Full Changelog](https://github.com/devlooped/stunts/compare/v1.0.0-alpha...v1.0.0-beta)

:sparkles: Implemented enhancements:

- Simplify behavior execution delegate signature [\#79](https://github.com/devlooped/stunts/issues/79)
- Improve argument collection API [\#74](https://github.com/devlooped/stunts/issues/74)
- Allow modifying method invocation return values [\#61](https://github.com/devlooped/stunts/issues/61)
- Don't force usage of a specific compiler toolset [\#58](https://github.com/devlooped/stunts/issues/58)
- Allow invoking base \(virtual\) method directly from IMethodInvocation [\#56](https://github.com/devlooped/stunts/issues/56)
- Add public API analyzers to improve API evolution [\#55](https://github.com/devlooped/stunts/issues/55)
- Provide simple property to opt out of static proxies [\#48](https://github.com/devlooped/stunts/issues/48)
- Remove dependency on Workspace API in the public API surface for avatar generator pipeline [\#41](https://github.com/devlooped/stunts/issues/41)
- ⚙ Support multiple compiler toolsets [\#59](https://github.com/devlooped/stunts/pull/59) (@kzu)

:bug: Fixed bugs:

- IMethodReturn.Outputs does not contain the out parameters [\#75](https://github.com/devlooped/stunts/issues/75)
- Source generator fails when used in Visual Studio preview [\#52](https://github.com/devlooped/stunts/issues/52)

:hammer: Other:

- Apply kzu/oss template for easier future maintenance [\#49](https://github.com/devlooped/stunts/issues/49)

:twisted_rightwards_arrows: Merged:

- Add public API analyzers, minor improvement to delegates/naming [\#80](https://github.com/devlooped/stunts/pull/80) (@kzu)
- Make arguments first-class and strong-typed [\#78](https://github.com/devlooped/stunts/pull/78) (@kzu)
-  ⌛ Run preview install in parallel, wait on build [\#70](https://github.com/devlooped/stunts/pull/70) (@kzu)
- ⭮ devlooped/oss + ♡ sponsors [\#69](https://github.com/devlooped/stunts/pull/69) (@kzu)
- 🖆 Apply kzu/oss template via dotnet-file [\#68](https://github.com/devlooped/stunts/pull/68) (@kzu)
- Make MethodReturn public to easily allow modifying return values [\#67](https://github.com/devlooped/stunts/pull/67) (@kzu)
- ◎ Simplify pipeline API by removing target, add callbase to IMethodInvocation [\#57](https://github.com/devlooped/stunts/pull/57) (@kzu)
- Rename properties to enable compile-time and run-time avatars [\#54](https://github.com/devlooped/stunts/pull/54) (@kzu)
- ⥱ Ensure compiler matches generator dependencies [\#53](https://github.com/devlooped/stunts/pull/53) (@kzu)
- 🖆 Apply kzu/oss template via dotnet-file [\#51](https://github.com/devlooped/stunts/pull/51) (@kzu)
- Use EnableCompiledAvatars/EnableDynamicAvatars properties [\#50](https://github.com/devlooped/stunts/pull/50) (@kzu)
- Generator pipeline API rework to remove dep on SyntaxGenerator/Document [\#42](https://github.com/devlooped/stunts/pull/42) (@kzu)
- Apply and enforce editorconfig rules during build [\#40](https://github.com/devlooped/stunts/pull/40) (@kzu)
- Normalize indentation \(spaces not tabs\) [\#34](https://github.com/devlooped/stunts/pull/34) (@stakx)
- Fix some typos [\#33](https://github.com/devlooped/stunts/pull/33) (@stakx)
- Add docfx-powered site, rather than jekyll. [\#32](https://github.com/devlooped/stunts/pull/32) (@kzu)

## [v1.0.0-alpha](https://github.com/devlooped/stunts/tree/v1.0.0-alpha) (2020-11-04)

[Full Changelog](https://github.com/devlooped/stunts/compare/v1.0.0-alpha.1...v1.0.0-alpha)

:hammer: Other:

- Add support for ref returns [\#23](https://github.com/devlooped/stunts/issues/23)
- Add proper namespace to generated types [\#22](https://github.com/devlooped/stunts/issues/22)
- Add support for nested types [\#21](https://github.com/devlooped/stunts/issues/21)

:twisted_rightwards_arrows: Merged:

- Make sure breakpoints on scenario source are active in debugging [\#31](https://github.com/devlooped/stunts/pull/31) (@kzu)
- Align DynamicProxy-based avatars with the static one [\#30](https://github.com/devlooped/stunts/pull/30) (@kzu)
- Improve constants naming for code actions and split processing [\#29](https://github.com/devlooped/stunts/pull/29) (@kzu)
- Add base class constructor overriding and interception [\#28](https://github.com/devlooped/stunts/pull/28) (@kzu)
- Remove VB from all source generation since it's not supported [\#27](https://github.com/devlooped/stunts/pull/27) (@kzu)
- Add base virtual method call support [\#26](https://github.com/devlooped/stunts/pull/26) (@kzu)
- Rename the project from Stunts to Avatar  [\#25](https://github.com/devlooped/stunts/pull/25) (@kzu)
- Dev \> Main [\#24](https://github.com/devlooped/stunts/pull/24) (@kzu)
- Make sure to also pack as buildTransitive for generators [\#20](https://github.com/devlooped/stunts/pull/20) (@kzu)
- Dev \> Main [\#19](https://github.com/devlooped/stunts/pull/19) (@kzu)
- Pack as SDK for extensibility scenarios [\#18](https://github.com/devlooped/stunts/pull/18) (@kzu)
- Switch from attribute-based registration to partial static ctor [\#17](https://github.com/devlooped/stunts/pull/17) (@kzu)
- Bundle DynamicProxy in Stunts itself for convenience [\#16](https://github.com/devlooped/stunts/pull/16) (@kzu)
- Replace codecov token and move to secret [\#15](https://github.com/devlooped/stunts/pull/15) (@kzu)
- Improvements to pipeline behavior and a few minor fixes [\#13](https://github.com/devlooped/stunts/pull/13) (@kzu)
- Coverage again [\#12](https://github.com/devlooped/stunts/pull/12) (@kzu)
- Build debug config on all builds, except for release [\#11](https://github.com/devlooped/stunts/pull/11) (@kzu)
- Turn on and report coverage across all platforms [\#10](https://github.com/devlooped/stunts/pull/10) (@kzu)
- Simplify how we calculate version suffix on GH CI [\#9](https://github.com/devlooped/stunts/pull/9) (@kzu)
- Remove private HashCode and switch to BCL one [\#8](https://github.com/devlooped/stunts/pull/8) (@kzu)
- Add GH pages pushing to main site repo [\#7](https://github.com/devlooped/stunts/pull/7) (@kzu)

## [v1.0.0-alpha.1](https://github.com/devlooped/stunts/tree/v1.0.0-alpha.1) (2020-10-15)

[Full Changelog](https://github.com/devlooped/stunts/compare/9fdd24a2c35651d2581af38209ae94e4398739a2...v1.0.0-alpha.1)

:twisted_rightwards_arrows: Merged:

- Fix tests reliability issues [\#6](https://github.com/devlooped/stunts/pull/6) (@kzu)
- Upgrade to RC2, add another sample [\#5](https://github.com/devlooped/stunts/pull/5) (@kzu)
- Add dependency resolution to the source generator itself [\#3](https://github.com/devlooped/stunts/pull/3) (@kzu)
- Samples and docs [\#2](https://github.com/devlooped/stunts/pull/2) (@kzu)
- Add sample test that passes in all supported platforms [\#1](https://github.com/devlooped/stunts/pull/1) (@kzu)



\* *This Changelog was automatically generated by [github_changelog_generator](https://github.com/github-changelog-generator/github-changelog-generator)*
