using System;
using Couchbase;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Dapper;

namespace SyncSqlToCb
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cluster = await Cluster.ConnectAsync("couchbase://localhost", "Administrator", "111111");

            var collection = (await cluster.BucketAsync("documents")).DefaultCollection();
            
            while(true)
            {
                Console.WriteLine("Fetching changes...");
                var cdtLastTrackingVersion = (await collection.ExistsAsync("LastTrackingVersionOfProduct")).Exists ?
                (await collection.GetAsync("LastTrackingVersionOfProduct")).ContentAs<long>() : default(long);

                long cdtNextTrackingVersion;
                using(var cnn = new SqlConnection("Server=localhost; Database=SQLToCouchbaseSample; User Id=sa; Password=yourStrong(!)Password"))
                {
                    cdtNextTrackingVersion = await cnn.ExecuteScalarAsync<long>("SELECT CHANGE_TRACKING_CURRENT_VERSION()");
                    var reader = await cnn.ExecuteReaderAsync("SELECT P.*, CT.SYS_CHANGE_OPERATION, CT.ProductId EntityId " + Environment.NewLine +
                                                "FROM Product AS P" + Environment.NewLine +
                                                $"RIGHT OUTER JOIN CHANGETABLE(CHANGES Product, {cdtLastTrackingVersion}) AS CT ON P.ProductId = CT.ProductId");
                    
                    while(await reader.ReadAsync())
                    {
                        var key = "Product-" + reader.GetInt32(reader.GetOrdinal("EntityId")).ToString();
                        var operation = reader.GetString(reader.GetOrdinal("SYS_CHANGE_OPERATION"));

                        if (operation == "I" || operation == "U")
                        {
                            await collection.UpsertAsync(key,
                                new {
                                    id = key,
                                    type = "Product",
                                    ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                                    ProductName = reader.GetString(reader.GetOrdinal("ProductName")),
                                    BrandId = reader.GetInt32(reader.GetOrdinal("BrandId")),
                                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                                    ModelYear = reader.GetInt32(reader.GetOrdinal("ModelYear")),
                                    ListPrice = reader.GetDecimal(reader.GetOrdinal("ListPrice"))
                                });
                            Console.WriteLine($"Operation (I/U) was synchronized: {key}");
                        }
                        else if(operation == "D")
                        {
                            if ((await collection.ExistsAsync(key)).Exists)
                            {
                                await collection.RemoveAsync(key);
                                Console.WriteLine($"Operation (D) was synchronized: {key}");
                            }
                        }
                    }
                }
                await collection.UpsertAsync<long>($"LastTrackingVersionOfProduct", cdtNextTrackingVersion);
                Console.WriteLine("Done.");
                Console.WriteLine("Waiting for next iteration...");
                System.Threading.Thread.Sleep(15000);
            }
        }
    }
}
