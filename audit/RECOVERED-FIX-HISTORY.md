# Recovered source-fix history

This index records the 50 source-fix commits recovered from the audit history after `b734c05944eeffccfde3208cd1ea45ef2ae401f9` and before the September 8 recovery. It is a commit index, not a count of distinct defects; some commits strengthen or supersede earlier repairs. Repair-request, withdrawal, and fixture commits are intentionally excluded. See AUDIT-CLOSEOUT.md for current dispositions and executed coverage.

| Commit | Historical source correction |
| --- | --- |
| `4aacc7add8be297d4a4d98fc4509606f07c45a22` | separate extended queue resolver lifecycle |
| `f542bd2b76eaa91d6b5a210cc2242fb1d0e3c36a` | separate downloader options from URL operands |
| `30189587aa5665a7da4350445a73fbede5ec4b7d` | keep first-run async work on a live UI loop |
| `d1c7c91b712708a0df0fa019409acc07bc369bec` | make updater checksum retry reusable |
| `969dbad1c83a8688c9ffa8184d28789a3bcd82d6` | preserve initial extended media options |
| `b3fe1f226d0532696537b88fc3f71e95385b557a` | synchronize extended downloader output messages |
| `1e6f937c7329827401384eecb11c7763b540eb6f` | require hashes for standalone app updates |
| `7b7886fe7050741d58a26226a9dd8618157166ba` | synchronize downloader output messages |
| `dbaa20e4f86141be2aa6f3753d7da85d5c41bc1c` | run batch downloader UI worker as STA |
| `e5b3d1f5df5f9a7721555091c92193585fef9252` | preserve extended downloader cancellation requests |
| `b6b85ecfa9c68aa2f998faa75dabe17bd1b874e3` | preserve quick download authentication |
| `636ac6deeb1fc28b2584c18f203fe58961d19730` | contain process-tree enumeration failures |
| `09ce211ca83bd2ca7f693dad4ae186a6e5905085` | always terminate failed download and conversion children |
| `9a7d94016efc9b3278c5b5640dd79507cfa91b82` | terminate extended downloader children on worker faults |
| `e7adc1da68cd1e96e848b5c827b31fa956534ac4` | preserve copied extended media type |
| `f0d9e8f67cef9fd9526ae98a645e073d6d0f9039` | apply extended audio VBR quality |
| `9601f9929d5a98d7581d5f5d0e1b82bc82a53e6f` | forward configured ffmpeg location for post-processing |
| `9d89883e3fe7f5bd3a73655c2bdedce340dbc489` | expose only supported extended audio formats |
| `1ec95a17f44c838a213f342c07c195f306887095` | enforce requested yt-dlp remux containers |
| `f4c138386134426bfeef67bad637085cf76ff4c2` | honor explicit standalone playlist selections |
| `514bbe42cfb6e555092c4474a89c3f7b60b6d98e` | preserve batch partial custom arguments |
| `4b068c212b57d92c4dc7c5dc4bbe2c3319a96d1c` | own the application update check gate |
| `37e754830a61422dda00ec142e23ba26563e289d` | bind cached update channel selection |
| `e998aceaf523e81c55d7ef62f9c4702b0f703d87` | key provider update cache by provider type |
| `00656f365194719fcf10e570dc2ec4269e418f33` | allow provider internal updater without release metadata |
| `2d1d7c48f255ab3c1208391b08467957669a11e8` | honor update-check cancellation during network retries |
| `a135a2efeee93ca82bd111957d4a7c30aeb06e01` | bound stalled HTTP body reads |
| `7be9d90b20e1cd0f0e8de17399b413e565dd4d00` | restore provider state when settings are cancelled |
| `b584c240d05f5992765397979b81392a6f70d98f` | surface INI persistence failures |
| `0382cb3c7786e727df586858b07b6beb9ec5f51a` | register processing forms after construction |
| `1ee26e72304f20c43edcfffc40324bc674c21023` | unregister localized forms on disposal |
| `97c0cd13212fe3d477259eda35932bc25d702ac2` | marshal localization changes to form threads |
| `9874be0467b981cfb485f0d8e8dd70bc46faf318` | preserve custom argument whitespace |
| `a7ea9bf9e49ab0effe8023ebe0be7964cdaab8cd` | tolerate busy clipboard in archive downloader |
| `98bd961ba9e156a14d7e044254d404343f2e2a18` | make confirmed ffmpeg overwrites noninteractive |
| `066d3ae3ed50444b1e9604e79e2d454167b35ff9` | resolve ImageMagick explicitly for GIF conversion |
| `7a591448ed109df032adc66f5e28d737764ce7bc` | bound and own metadata probe lifecycles |
| `ebc0fd5f57420dbcda9d1b651456710600c3f43e` | move extended thumbnail retrieval off the UI thread |
| `22ca85783576edf83813da2eb8074f5046f77169` | probe merger inputs off the UI thread |
| `79fa2d931b229aca71958e3b8ad43714161b93d0` | wait for GIF frame extraction off the UI thread |
| `ccbff3ea962bc0b4d5db44355a68589559547ad7` | preserve UNC roots in media source paths (H001) |
| `5632a47683763ad1137f18a5f6c21d30f47537bb` | serialize millisecond time offsets without changing their magnitude (H004) |
| `01f2cc70f94394c21315e9c276709d6d0ac6570a` | include separators within bounded joins (H005) |
| `dc2139faf17896bd770ce8350c8f8171209a3a35` | validate forward time ranges and implement value equality (H006) |
| `a0ddad33cd41d093d3fcbfbc2f964b880a8268c9` | preserve URLs and quoted slashes in translation values (D034) |
| `bd75617fd72b126a375e81fb5cc67d89b6209afd` | bound and cancel owned metadata probes including pipe draining (D006 D007 R006) |
| `13eefec54597c57566390f73641dd5ecb7de18e1` | keep cancellation terminal until the next transfer starts (D005) |
| `4f688418999f9380a215b0b4e277d7b6e12cc067` | guard active transfer ownership independently of progress status (D001) |
| `869cebd4c71fea1c9fd30c740d18471a6944dc48` | bound live process output and own transfer child lifetimes |
| `e8075e29206881274ca5e6bcd8ed8a0e1367e8d4` | complete cooperative transfer cancellation before closing forms |
