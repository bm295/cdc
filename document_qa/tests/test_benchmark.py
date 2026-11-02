import unittest
from document_qa.benchmark import percentile, summarize, benchmark


class BenchmarkTests(unittest.TestCase):
    def test_nearest_rank_and_missing_population(self):
        self.assertIsNone(percentile([], .95))
        self.assertEqual(percentile(list(range(1,101)),.5),50)
        self.assertEqual(percentile(list(range(1,101)),.95),95)
        self.assertEqual(percentile([7],.95),7)

    def test_errors_are_not_successful_throughput_or_latency(self):
        result = summarize([{'status':200,'latency_ms':10},
                            {'status':429,'latency_ms':1},
                            {'status':'transport_error','latency_ms':100}],2,3.6)
        self.assertEqual(result['successful_requests_per_second'],.5)
        self.assertEqual(result['completed_requests_per_second'],1.5)
        self.assertEqual(result['http_latency_success']['p95_ms'],10)
        self.assertEqual(result['http_latency_all']['p95_ms'],100)
        self.assertEqual(result['cost']['inference_cost_usd'],0)
        self.assertAlmostEqual(result['cost']['estimated_infrastructure_window_usd'],.002)
        self.assertIsNone(summarize([],1)['http_latency_success']['p50_ms'])
        with self.assertRaises(ValueError):
            summarize([],1,float('nan'))

    def test_real_http_smoke(self):
        report = benchmark(requests=3,concurrency=2,warmup=1)
        self.assertEqual(report['summary']['succeeded'],3)
        self.assertGreater(report['summary']['http_latency_success']['p50_ms'],0)
