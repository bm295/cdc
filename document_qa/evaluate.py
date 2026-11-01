"""Deterministic retrieval metrics and lexical answer proxies, not LLM judging."""
import argparse
import json
from pathlib import Path
from .core import KnowledgeBase, tokens


def score(case, result):
    relevant = set(case['relevant_documents'])
    retrieved = [c['document_id'] for c in result['citations']]
    recall = len(relevant & set(retrieved)) / len(relevant) if relevant else None
    precision = (sum(d in relevant for d in retrieved) / len(retrieved) if retrieved else 0.0) if relevant else None
    rr = next((1 / (i+1) for i, doc in enumerate(retrieved) if doc in relevant), 0.0) if relevant else None
    expected = set(tokens(case['reference_answer']))
    actual = set(tokens(result['answer'])) if not result['abstained'] else set()
    f1 = 2 * len(expected & actual) / (len(expected)+len(actual)) if expected or actual else 1.0
    grounded = result['answer'] == '\n\n'.join(f"[{c['label']}] {c['quote']}" for c in result['citations']) if result['citations'] else result['abstained']
    return {'recall_at_k': recall, 'precision_at_k': precision, 'mrr': rr,
            'answer_token_f1': f1, 'extractive_integrity': float(grounded),
            'abstention_accuracy': float(result['abstained'] == case['should_abstain']),
            'latency_ms': result['latency_ms']}


def evaluate(dataset, root, top_k=3):
    if not dataset.get('cases'):
        raise ValueError('Evaluation requires at least one case')
    kb = KnowledgeBase(':memory:')
    for document in dataset['documents']:
        kb.ingest(document['id'], document['title'], (root / document['path']).read_text(encoding='utf-8'))
    ids = {d['id'] for d in dataset['documents']}
    rows = []
    for case in dataset['cases']:
        if not set(case['relevant_documents']) <= ids:
            raise ValueError('Unknown relevant document')
        if case['should_abstain'] != (not case['relevant_documents']):
            raise ValueError('Abstention cases must have no relevant documents')
        response = kb.ask(case['question'], top_k)
        rows.append({'id': case['id'], 'metrics': score(case, response), 'response': response})
    summary = {}
    for name in rows[0]['metrics']:
        values = [r['metrics'][name] for r in rows if r['metrics'][name] is not None]
        summary[name] = sum(values)/len(values) if values else None
    thresholds = dataset.get('thresholds', {})
    passed = all(summary.get(key) is not None and summary[key] >= value for key, value in thresholds.items())
    kb.db.close()
    return {'top_k': top_k, 'cases': rows, 'summary': summary, 'thresholds': thresholds, 'passed': passed}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dataset', type=Path, default=Path(__file__).with_name('data') / 'evaluation.json')
    parser.add_argument('--output', type=Path, default=Path('artifacts/document-qa-evaluation.json'))
    parser.add_argument('--top-k', type=int, default=3)
    args = parser.parse_args()
    report = evaluate(json.loads(args.dataset.read_text()), Path(__file__).resolve().parents[1], args.top_k)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'summary': report['summary'], 'passed': report['passed']}, indent=2))
    raise SystemExit(0 if report['passed'] else 1)


if __name__ == '__main__':
    main()
