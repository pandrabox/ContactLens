# ContactLens System Specification

## 概要
Unity用コンタクトレンズシステム。プレハブをキャラクター直下に配置することで目のテクスチャを変更し、外すと元に戻る。

## 対象プラットフォーム
- Unity / VRChat

---

## 機能一覧

### 実装済み

#### コアシステム
- プレハブをキャラクター直下に配置 → 目のテクスチャ変更
- プレハブを外す → 元のテクスチャに復元
- VRChatビルド対応（ビルド時にRestore抑制）

#### アバターモード
- **Ver1&2**: 瞳孔がテクスチャのみのアバター
- **Ver3&If**: 瞳孔が独立メッシュのアバター

#### 瞳孔設定
- 瞳孔ON/OFF
- 瞳孔の色変更
- 瞳孔の透過度（α）設定

#### 瞳孔Hide機能（Ver3&If専用）
- **条件**: Ver3&Ifモードで瞳孔OFFまたはα<1
- **動作**: 瞳孔メッシュの頂点を頭内部に移動（非表示化）
- **方式**: アイランド検出（100頂点、Y座標-0.19〜-0.175）で瞳孔を特定
- **利点**: シェーダー変更不要、BlendShape影響なし、低侵襲

#### サムネイル表示
- Projectビューでprefabに`_thumb.png`サムネイルを表示
- キャッシュ機能付き

#### RemoveAll機能
- シーン内の全ContactLensを削除
- 壊れたBodyをRevertで自動復旧
- Generatedフォルダのクリーンアップ

---

### 開発中

#### 1. サムネイル自動生成機能
- **目的**: 製作者モードのリリース時に、レンズ適用後のプレビュー画像を自動生成する
- **トリガー**: 製作者モードでContactLensコンポーネントの「リリース」ボタン押下時
- **サイズ**: 256x256（正方形、透過PNG）

#### 2. カタログ表示・検索機能
- **目的**: Projectに存在するレンズを一覧表示し、簡単に選択できるようにする
- **アクセス方法**: メニュー「Pan/ContactLens」から
- **表示形式**: サムネイルのグリッド表示
- **検索・フィルタ**: アバター別フィルタ

#### 3. 製作者モード
- **目的**: テクスチャ作成者が配布用unitypackageを簡単に作成できる
- **メニュー**: Pan/ContactLens/製作者モード

---

## 技術仕様

### 瞳孔Hide（頂点移動方式）
1. Union-Findでメッシュのアイランドを検出
2. 100頂点かつY座標-0.19〜-0.175のアイランドを瞳孔と判定
3. 該当頂点を(0, 0, 1.5)に移動（頭内部）
4. 修正メッシュをアセットとして保存

### 生成ファイル
Generatedフォルダに以下を生成：
- `{avatar}_lens_{timestamp}.mat` - 合成済みマテリアル
- `{avatar}_main_{timestamp}.png` - メインテクスチャ
- `{avatar}_emission_{timestamp}.png` - エミッションテクスチャ
- `{avatar}_mesh_{timestamp}.asset` - 瞳孔Hide済みメッシュ

---

## ファイル構成

```
ContactLens/
├── script/
│   ├── ContactLens.cs           # メインコンポーネント
│   ├── com.github.pandrabox.contactlens.asmdef
│   └── Editor/
│       ├── ContactLensEditor.cs      # インスペクタUI
│       ├── ContactLensMenu.cs        # メニューコマンド
│       ├── ContactLensThumbnail.cs   # サムネイル表示
│       ├── ContactLensVRCCallback.cs # VRCビルド対応
│       └── com.github.pandrabox.contactlens.Editor.asmdef
├── texture/
│   ├── Mask/
│   │   ├── EyeMask.png      # 目の範囲マスク
│   │   ├── pupil_1_2.png    # Ver1&2用瞳孔マスク
│   │   └── pupil_3_if.png   # Ver3&If用瞳孔マスク
│   └── Sample.png           # サンプルテクスチャ
├── Generated/               # 生成ファイル（git除外）
├── Sample(1,2).prefab       # Ver1&2用サンプル
├── Sample(3,If).prefab      # Ver3&If用サンプル
└── spec.md                  # この仕様書
```

---

作成日: 2026-01-10
更新日: 2026-02-03
