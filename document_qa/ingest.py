"""Import UTF-8 Markdown/text documents through the same ingestion pipeline."""
import argparse
from pathlib import Path
from .core import KnowledgeBase


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('paths', nargs='+', type=Path)
    parser.add_argument('--db', default='knowledge.db')
    args = parser.parse_args()
    kb = KnowledgeBase(args.db)
    try:
        for path in args.paths:
            if path.suffix.lower() not in ('.md', '.txt'):
                parser.error('Only UTF-8 .md and .txt documents are supported')
            print(kb.ingest(path.as_posix(), path.name, path.read_text(encoding='utf-8')))
    finally:
        kb.db.close()


if __name__ == '__main__':
    main()
