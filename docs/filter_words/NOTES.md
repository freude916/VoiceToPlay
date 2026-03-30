# OpenFST 与 Jieba 使用笔记

## OpenFST - 从 Vosk 模型提取词汇

### 环境

- Kaldi OpenFST 位于 `/opt/kaldi/tools/openfst-1.8.4/`
- 需要设置 `LD_LIBRARY_PATH` 包含 lib 和 lib/fst 目录

### 常用命令

```bash
# 设置环境变量
export LD_LIBRARY_PATH=/opt/kaldi/tools/openfst-1.8.4/lib:/opt/kaldi/tools/openfst-1.8.4/lib/fst

# 查看 FST 文件信息
/opt/kaldi/tools/openfst-1.8.4/bin/fstinfo <fst_file>

# 打印 FST 内容（如果有内嵌符号表）
/opt/kaldi/tools/openfst-1.8.4/bin/fstprint <fst_file>

# 打印 FST 内容（使用外部符号表）
/opt/kaldi/tools/openfst-1.8.4/bin/fstprint --isymbols=words.txt --osymbols=words.txt <fst_file>
```

### Vosk 模型结构

```
vosk-model-small-cn-0.22/
├── am/final.mdl          # 声学模型
├── graph/
│   ├── Gr.fst            # 语法FST（词汇→词，有内嵌符号表）
│   ├── HCLr.fst          # HMM+Context+Lexicon FST（音素级，无符号表）
│   └── phones/
│       └── word_boundary.int  # 音素边界标记
└── ivector/              # 说话人适应相关
```

### 提取词汇表

```bash
# 从 Gr.fst 提取所有词汇
fstprint Gr.fst | awk '{print $3}' | grep -v '^<eps>$' | sort -u > words.txt
```

### FST 类型

| 类型               | 说明                       |
|------------------|--------------------------|
| ngram            | 语言模型 FST，Gr.fst 常见       |
| olabel_lookahead | 带输出标签预查的 FST，HCLr.fst 常见 |
| standard         | 标准 FST                   |

---

## Jieba 分词器

### 自定义词典

#### 方法1: 加载用户词典（追加到默认词典）

```python
import jieba
jieba.load_userdict("userdict.txt")
```

#### 方法2: 完全替换默认词典

```python
import jieba
jieba.set_dictionary("/path/to/custom_dict.txt")
```

#### 方法3: 创建独立分词器

```python
from jieba import Tokenizer
tokenizer = Tokenizer(dictionary="/path/to/custom_dict.txt")
tokenizer.lcut("待分词文本")
```

### 词典文件格式

#### dict.txt - 主词典

```
词语 词频 词性
的 3188252 uj
了 883634 ul
...
```

#### idf.txt - IDF 权重（用于关键词提取）

```
词语 IDF值
劳动防护 13.900677652
...
```

#### stopwords.txt - 停用词

```
词语（每行一个）
的
了
...
```

#### cn_synonym.txt - 同义词

```
编号= 词1 词2 词3 ...
Aa01A01= 人 士 人物 人士
...
```

### 注意事项

1. **词频不能为 None** - 可以省略词频，但不能写成 `None`
2. **分隔符为单个空格** - 词语、词频、词性之间用空格分隔
3. **词性可选** - 不指定词性时只写词语和词频
4. **用户词典优先级** - `load_userdict` 加载的词会覆盖默认词典中的同词

---

## 完整工作流：为 Vosk 小模型过滤 Jieba 词库

### 1. 提取小模型词汇

```bash
LD_LIBRARY_PATH=/opt/kaldi/tools/openfst-1.8.4/lib:/opt/kaldi/tools/openfst-1.8.4/lib/fst \
/opt/kaldi/tools/openfst-1.8.4/bin/fstprint \
  /path/to/vosk-model/graph/Gr.fst \
  | awk '{print $3}' \
  | grep -v '^<eps>$' \
  | sort -u > small_model_words.txt
```

### 2. 过滤 Jieba 词库

```python
# 见 extract_vocab.py
```

### 3. 使用过滤后的词库

直接覆盖了原来的 Resources 。
