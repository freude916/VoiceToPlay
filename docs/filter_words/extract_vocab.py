#!/usr/bin/env python3
"""
过滤 jieba 词库，只保留 Vosk 小模型支持的词汇
"""
from pathlib import Path


def load_supported_words(base_dir: Path) -> set:
    """加载小模型支持的词汇"""
    small_model_words_path = base_dir / "small_model_words.txt"
    print(f"加载小模型词表: {small_model_words_path}")
    
    supported_words = set()
    with open(small_model_words_path, 'r', encoding='utf-8') as f:
        for line in f:
            word = line.strip()
            if word:
                supported_words.add(word)
    
    print(f"小模型支持词汇数: {len(supported_words)}")
    return supported_words


def filter_dict_txt(supported_words: set, jieba_dir: Path, output_dir: Path):
    """
    过滤 dict.txt
    格式: 词 频率 词性
    """
    input_path = jieba_dir / "dict.txt"
    output_path = output_dir / "dict.txt"
    
    total, kept = 0, 0
    with open(input_path, 'r', encoding='utf-8') as fin, \
         open(output_path, 'w', encoding='utf-8') as fout:
        
        for line in fin:
            total += 1
            parts = line.strip().split()
            if not parts:
                continue
            
            word = parts[0]
            if word in supported_words:
                fout.write(line)
                kept += 1
    
    print(f"dict.txt: {kept}/{total} ({kept/total*100:.1f}%)")
    return kept, total


def filter_idf_txt(supported_words: set, jieba_dir: Path, output_dir: Path):
    """
    过滤 idf.txt
    格式: 词 idf值
    """
    input_path = jieba_dir / "idf.txt"
    output_path = output_dir / "idf.txt"
    
    total, kept = 0, 0
    with open(input_path, 'r', encoding='utf-8') as fin, \
         open(output_path, 'w', encoding='utf-8') as fout:
        
        for line in fin:
            total += 1
            parts = line.strip().split()
            if not parts:
                continue
            
            word = parts[0]
            if word in supported_words:
                fout.write(line)
                kept += 1
    
    print(f"idf.txt: {kept}/{total} ({kept/total*100:.1f}%)")
    return kept, total


def filter_stopwords_txt(supported_words: set, jieba_dir: Path, output_dir: Path):
    """
    过滤 stopwords.txt
    格式: 词（每行一个）
    注意: stopwords 包含英文，只过滤中文部分
    """
    input_path = jieba_dir / "stopwords.txt"
    output_path = output_dir / "stopwords.txt"
    
    total, kept = 0, 0
    with open(input_path, 'r', encoding='utf-8') as fin, \
         open(output_path, 'w', encoding='utf-8') as fout:
        
        for line in fin:
            total += 1
            word = line.strip()
            if not word:
                continue
            
            # 英文停用词保留
            if word.isascii():
                fout.write(line)
                kept += 1
            # 中文停用词检查是否在小模型词表中
            elif word in supported_words:
                fout.write(line)
                kept += 1
    
    print(f"stopwords.txt: {kept}/{total} ({kept/total*100:.1f}%)")
    return kept, total


def filter_cn_synonym_txt(supported_words: set, jieba_dir: Path, output_dir: Path):
    """
    过滤 cn_synonym.txt
    格式: 编号= 词1 词2 词3 ...
    只保留所有词都在词表中的行
    """
    input_path = jieba_dir / "cn_synonym.txt"
    output_path = output_dir / "cn_synonym.txt"
    
    total, kept = 0, 0
    with open(input_path, 'r', encoding='utf-8') as fin, \
         open(output_path, 'w', encoding='utf-8') as fout:
        
        for line in fin:
            total += 1
            parts = line.strip().split()
            if len(parts) < 2:
                continue
            
            # 格式: Aa01A01= 人 士 人物 ...
            # parts[0] 是编号，parts[1:] 是同义词
            words = parts[1:]
            
            # 只保留所有词都在词表中的行
            if all(w in supported_words for w in words):
                fout.write(line)
                kept += 1
    
    print(f"cn_synonym.txt: {kept}/{total} ({kept/total*100:.1f}%)")
    return kept, total


def main():
    base_dir = Path("/home/zed/Documents/tmp/v2p")
    jieba_dir = base_dir / "jieba"
    output_dir = base_dir / "filtered_jieba"
    
    output_dir.mkdir(exist_ok=True)
    
    print("=" * 60)
    print("过滤 jieba 词库")
    print("=" * 60)
    
    # 加载小模型词表
    supported_words = load_supported_words(base_dir)
    
    print(f"\n输出目录: {output_dir}")
    print("-" * 40)
    
    # 过滤各个文件
    filter_dict_txt(supported_words, jieba_dir, output_dir)
    filter_idf_txt(supported_words, jieba_dir, output_dir)
    filter_stopwords_txt(supported_words, jieba_dir, output_dir)
    filter_cn_synonym_txt(supported_words, jieba_dir, output_dir)
    
    # 复制其他必要文件（不需要过滤的 JSON 文件）
    import shutil
    json_files = ["char_state_tab.json", "pos_prob_emit.json", 
                  "pos_prob_start.json", "pos_prob_trans.json",
                  "prob_emit.json", "prob_trans.json"]
    
    print("-" * 40)
    print("复制 JSON 文件...")
    for f in json_files:
        src = jieba_dir / f
        dst = output_dir / f
        if src.exists():
            shutil.copy(src, dst)
            print(f"  {f}")
    
    print("\n完成!")


if __name__ == "__main__":
    main()
