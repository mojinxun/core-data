﻿using Dapper;
using MySql.Data.MySqlClient;
using Overt.Core.Data.Expressions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Overt.Core.Data
{
    /// <summary>
    /// Dapper扩展
    /// </summary>
    public static partial class DapperExtensions
    {
        #region TableName
        /// <summary>
        /// 获取主表名称
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static string GetMainTableName(this Type entity)
        {
            var attribute = entity.GetAttribute<TableAttribute>();
            string mTableName;
            if (attribute == null)
                mTableName = entity.Name;
            else
                mTableName = attribute.Name;
            return mTableName;
        }

        /// <summary>
        /// 获取表名
        /// </summary>
        /// <param name="val"></param>
        /// <param name="tableNameFunc"></param>
        /// <returns></returns>
        public static string GetTableName<TEntity>(this string val, Func<string> tableNameFunc = null) where TEntity : class, new()
        {
            if (tableNameFunc != null)
                return tableNameFunc.Invoke();

            var t = typeof(TEntity);
            var mTableName = t.GetMainTableName();
            var propertyInfo = t.GetProperty<SubmeterAttribute>();
            if (propertyInfo == null) // 代表没有分表特性
                return mTableName;

            // 获取分表
            var suffix = propertyInfo.GetSuffix(val);
            return $"{mTableName}_{suffix}";
        }

        /// <summary>
        /// 获取表名
        /// </summary>
        /// <param name="entity">实体实例</param>
        /// <param name="tableNameFunc"></param>
        /// <returns></returns>
        public static string GetTableName<TEntity>(this TEntity entity, Func<string> tableNameFunc = null) where TEntity : class, new()
        {
            if (tableNameFunc != null)
                return tableNameFunc.Invoke();

            var t = typeof(TEntity);
            var mTableName = t.GetMainTableName();
            var propertyInfo = t.GetProperty<SubmeterAttribute>();
            if (propertyInfo == null) // 代表没有分表特性
                return mTableName;

            // 获取分表
            var suffix = propertyInfo.GetSuffix(entity);
            return $"{mTableName}_{suffix}";
        }

        /// <summary>
        /// 获取表名
        /// </summary>
        /// <param name="expression">表达式数据</param>
        /// <param name="tableNameFunc"></param>
        /// <returns></returns>
        public static string GetTableName<TEntity>(this Expression<Func<TEntity, bool>> expression, Func<string> tableNameFunc = null) where TEntity : class, new()
        {
            if (tableNameFunc != null)
                return tableNameFunc.Invoke();

            var t = typeof(TEntity);
            var mTableName = t.GetMainTableName();
            var propertyInfo = t.GetProperty<SubmeterAttribute>();
            if (propertyInfo == null) // 代表没有分表特性
                return mTableName;

            // 获取分表
            var suffix = propertyInfo.GetSuffix(expression);
            return $"{mTableName}_{suffix}";
        }
        #endregion

        #region Field
        /// <summary>
        /// 获取自增字段
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static PropertyInfo GetIdentityField(this Type type)
        {
            var propertyInfos = type.GetProperties<DatabaseGeneratedAttribute>();
            if ((propertyInfos?.Count ?? 0) <= 0) // 代表没有主键
                return null;

            foreach (var pi in propertyInfos)
            {
                var attribute = pi.GetAttribute<DatabaseGeneratedAttribute>();
                if (attribute != null && attribute.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                {
                    return pi;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取自定义类型的字段列表
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static List<PropertyInfo> GetCustomFields(this Type type)
        {
            var propertyInfos = type.GetProperties<DataTypeAttribute>();
            if ((propertyInfos?.Count ?? 0) <= 0) // 代表没有自定义字段
                return null;

            var result = new List<PropertyInfo>();
            foreach (var pi in propertyInfos)
            {
                var attribute = pi.GetAttribute<DataTypeAttribute>();
                if (attribute != null)
                {
                    result.Add(pi);
                }
            }
            return result;
        }
        #endregion

        #region Public Method
        /// <summary>
        /// 是否存在表
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="outSqlAction"></param>
        /// <returns></returns>
        public static bool IsExistTable(this IDbConnection connection, string tableName, Action<string> outSqlAction = null)
        {
            if (string.IsNullOrEmpty(tableName))
                return false;
            var dbType = connection.GetDbType();
            var dbName = connection.Database;
            var sql = dbType.ExistTableSql(dbName, tableName);
            outSqlAction?.Invoke(sql); // 返回sql

            var result = connection.QueryFirstOrDefault<int>(sql);
            return result > 0;
        }

        /// <summary>
        /// 是否存在字段
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="fieldName"></param>
        /// <param name="outSqlAction"></param>
        /// <returns></returns>
        public static bool IsExistField(this IDbConnection connection, string tableName, string fieldName, Action<string> outSqlAction = null)
        {
            if (string.IsNullOrEmpty(tableName))
                return false;
            var dbType = connection.GetDbType();
            var dbName = connection.Database;
            var sql = dbType.ExistFieldSql(dbName, tableName, fieldName);
            outSqlAction?.Invoke(sql);

            var result = connection.QueryFirstOrDefault<int>(sql);
            return result > 0;
        }

        /// <summary>
        /// 插入数据
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="entity"></param>
        /// <param name="returnLastIdentity">是否返回自增的数据</param>
        /// <param name="outSqlAction">返回sql语句</param>
        /// <returns>-1 参数为空</returns>
        public static int Insert<TEntity>(this
            IDbConnection connection,
            string tableName,
            TEntity entity,
            bool returnLastIdentity = false,
            Action<string> outSqlAction = null)
            where TEntity : class, new()
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var dbType = connection.GetDbType();
            var sqlExpression = SqlExpression.Insert<TEntity>(dbType, tableName, returnLastIdentity);
            outSqlAction?.Invoke(sqlExpression.Script);

            int result;
            var identityPI = typeof(TEntity).GetIdentityField();
            if (identityPI != null && returnLastIdentity)
            {
                result = connection.ExecuteScalar<int>(sqlExpression.Script, entity);
                if (result > 0)
                    identityPI.SetValue(entity, result);
            }
            else
            {
                result = connection.Execute(sqlExpression.Script, entity);
            }

            return result;
        }

        /// <summary>
        /// 批量插入数据
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="entities"></param>
        /// <param name="outSqlAction">返回sql语句</param>
        /// <returns>-1 参数为空</returns>
        public static int Insert<TEntity>(this
            IDbConnection connection,
            string tableName,
            IEnumerable<TEntity> entities,
            Action<string> outSqlAction = null)
            where TEntity : class, new()
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if ((entities?.Count() ?? 0) <= 0)
                throw new ArgumentNullException(nameof(entities));

            var dbType = connection.GetDbType();
            var sqlExpression = SqlExpression.Insert<TEntity>(dbType, tableName);
            outSqlAction?.Invoke(sqlExpression.Script);

            var result = connection.Execute(sqlExpression.Script, entities);
            return result;
        }

        /// <summary>
        /// 删除数据
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="whereExpress"></param>
        /// <param name="outSqlAction">返回sql语句</param>
        /// <returns>-1 参数为空</returns>
        public static int Delete<TEntity>(this
            IDbConnection connection,
            string tableName,
            Expression<Func<TEntity, bool>> whereExpress,
            Action<string> outSqlAction = null)
            where TEntity : class, new()
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if (whereExpress == null)
                throw new ArgumentNullException(nameof(whereExpress));

            var dbType = connection.GetDbType();
            var sqlExpression = SqlExpression.Delete<TEntity>(dbType, tableName).Where(whereExpress);
            outSqlAction?.Invoke(sqlExpression.Script);

            var result = connection.Execute(sqlExpression.Script, sqlExpression.DbParams);
            return result;
        }

        /// <summary>
        /// 对象修改
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="entity"></param>
        /// <param name="fields">选择字段</param>
        /// <param name="outSqlAction">返回sql语句</param>
        /// <returns></returns>
        public static bool Set<TEntity>(this
            IDbConnection connection,
            string tableName,
            TEntity entity,
            IEnumerable<string> fields = null,
            Action<string> outSqlAction = null)
            where TEntity : class, new()
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var dbType = connection.GetDbType();
            var sqlExpression = SqlExpression.Update<TEntity>(dbType, fields, tableName);
            outSqlAction?.Invoke(sqlExpression.Script);

            var result = connection.Execute(sqlExpression.Script, entity);
            return result > 0;
        }

        /// <summary>
        /// 条件修改
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="connection">连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="setExpress">修改内容表达式</param>
        /// <param name="whereExpress">条件表达式</param>
        /// <param name="outSqlAction">返回sql语句</param>
        /// <returns></returns>
        public static bool Set<TEntity>(this
            IDbConnection connection,
            string tableName,
            Expression<Func<object>> setExpress,
            Expression<Func<TEntity, bool>> whereExpress,
            Action<string> outSqlAction = null)
            where TEntity : class, new()
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if (setExpress == null || whereExpress == null)
                throw new ArgumentNullException($"{nameof(setExpress)} / {nameof(whereExpress)}");

            var dbType = connection.GetDbType();
            var sqlExpression = SqlExpression.Update<TEntity>(dbType, setExpress, tableName).Where(whereExpress);
            outSqlAction?.Invoke(sqlExpression.Script); // 返回sql

            var result = connection.Execute(sqlExpression.Script, sqlExpression.DbParams);
            return result > 0;
        }

        /// <summary>
        /// 条件修改 在字段上增减
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="connection">连接</param>
        /// <param name="tableName">表名</param>
        /// <param name="field">增减的字段</param>
        /// <param name="value">增减的值</param>
        /// <param name="whereExpress">条件表达式</param>
        /// <param name="outSqlAction">返回sql语句</param>
        /// <returns></returns>
        public static bool Incr<TEntity, TValue>(this
            IDbConnection connection,
            string tableName,
            string field,
            TValue value,
            Expression<Func<TEntity, bool>> whereExpress,
            Action<string> outSqlAction = null)
            where TEntity : class, new()
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(field))
                throw new ArgumentNullException(field, "增减字段不能为空");

            var dbType = connection.GetDbType();
            var setExpressString = $"{field.ParamSql(dbType)} = {field.ParamSql(dbType)} + ({value})";
            var sqlExpression = SqlExpression.Update<TEntity>(dbType, () => setExpressString, tableName).Where(whereExpress);
            outSqlAction?.Invoke(sqlExpression.Script); // 返回sql

            var result = connection.Execute(sqlExpression.Script, sqlExpression.DbParams);
            return result > 0;
        }

        /// <summary>
        /// 获取单条数据
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="connection"></param>
        /// <param name="tableName">表名</param>
        /// <param name="whereExpress">条件表达式</param>
        /// <param name="fieldExpress">选择字段，默认为*</param>
        /// <param name="outSqlAction">返回sql语句</param>
        /// <returns></returns>
        public static TEntity Get<TEntity>(this
            IDbConnection connection,
            string tableName,
            Expression<Func<TEntity, bool>> whereExpress,
            Expression<Func<TEntity, object>> fieldExpress = null,
            Action<string> outSqlAction = null)
            where TEntity : class, new()
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if (whereExpress == null)
                throw new ArgumentNullException(nameof(whereExpress));

            var dbType = connection.GetDbType();
            var sqlExpression = SqlExpression.Select(dbType, fieldExpress, tableName).Where(whereExpress);
            outSqlAction?.Invoke(sqlExpression.Script); // 返回sql

            var result = connection.QueryFirstOrDefault<TEntity>(sqlExpression.Script, sqlExpression.DbParams);
            return result;
        }

        /// <summary>
        /// 获取分页数据
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="page"></param>
        /// <param name="rows"></param>
        /// <param name="whereExpress">条件表达式</param>
        /// <param name="fieldExpress">选择字段，默认为*</param>
        /// <param name="orderByFields">排序字段集合</param>
        /// <param name="outSqlAction">返回sql语句</param>
        /// <returns></returns>
        public static IEnumerable<TEntity> GetList<TEntity>(this
            IDbConnection connection,
            string tableName,
            int page,
            int rows,
            Expression<Func<TEntity, bool>> whereExpress,
            Expression<Func<TEntity, object>> fieldExpress = null,
            List<OrderByField> orderByFields = null,
            Action<string> outSqlAction = null)
            where TEntity : class, new()
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            var dbType = connection.GetDbType();
            var sqlExpression = SqlExpression.Select(dbType, fieldExpress, tableName);
            if (whereExpress != null)
                sqlExpression.Where(whereExpress);

            var orderBy = string.Empty;
            if ((orderByFields?.Count ?? 0) > 0)
                orderBy = $" {string.Join(", ", orderByFields.Select(oo => oo.Field.ParamSql(dbType) + " " + oo.OrderBy))}";
            sqlExpression.OrderBy(orderBy).Limit(page, rows);
            outSqlAction?.Invoke(sqlExpression.Script); // 返回sql

            var result = connection.Query<TEntity>(sqlExpression.Script, sqlExpression.DbParams);
            return result;
        }

        /// <summary>
        /// 获取分页数据 Offset
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <param name="whereExpress">条件表达式</param>
        /// <param name="fieldExpress">选择字段，默认为*</param>
        /// <param name="orderByFields">排序字段集合</param>
        /// <param name="outSqlAction">返回sql语句</param>
        /// <returns></returns>
        public static IEnumerable<TEntity> GetOffsets<TEntity>(this
            IDbConnection connection,
            string tableName,
            int offset,
            int size,
            Expression<Func<TEntity, bool>> whereExpress,
            Expression<Func<TEntity, object>> fieldExpress = null,
            List<OrderByField> orderByFields = null,
            Action<string> outSqlAction = null)
            where TEntity : class, new()
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            var dbType = connection.GetDbType();
            var sqlExpression = SqlExpression.Select(dbType, fieldExpress, tableName);
            if (whereExpress != null)
                sqlExpression.Where(whereExpress);

            var orderBy = string.Empty;
            if ((orderByFields?.Count ?? 0) > 0)
                orderBy = $" {string.Join(", ", orderByFields.Select(oo => oo.Field.ParamSql(dbType) + " " + oo.OrderBy))}";
            sqlExpression.OrderBy(orderBy).Offset(offset, size);
            outSqlAction?.Invoke(sqlExpression.Script); // 返回sql

            var result = connection.Query<TEntity>(sqlExpression.Script, sqlExpression.DbParams);
            return result;
        }

        /// <summary>
        /// 获取数量
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="whereExpress">条件表达式</param>
        /// <param name="outSqlAction">返回sql语句</param>
        /// <returns></returns>
        public static int Count<TEntity>(this
            IDbConnection connection,
            string tableName,
            Expression<Func<TEntity, bool>> whereExpress,
            Action<string> outSqlAction = null)
            where TEntity : class, new()
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            var dbType = connection.GetDbType();
            var sqlExpression = SqlExpression.Count<TEntity>(dbType, tableName: tableName).Where(whereExpress);
            outSqlAction?.Invoke(sqlExpression.Script); // 返回sql

            var result = connection.QueryFirstOrDefault<int>(sqlExpression.Script, sqlExpression.DbParams);
            return result;
        }
        #endregion

        #region Private Method
        static ConcurrentDictionary<string, DatabaseType> MSSqlDbType = new ConcurrentDictionary<string, DatabaseType>();
        /// <summary>
        /// 获取db类型
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        internal static DatabaseType GetDbType(this IDbConnection connection)
        {
            if (connection is MySqlConnection)
                return DatabaseType.MySql;
            if (connection is SqlConnection)
            {
                return MSSqlDbType.GetOrAdd(connection.ConnectionString, (connectionString) =>
                {
                    var sqlConnection = (SqlConnection)connection;
                    var v = sqlConnection.ServerVersion;
                    int.TryParse(v.Substring(0, v.IndexOf(".")), out int bV);
                    if (bV >= Constants.MSSQLVersion.SQLServer2012Bv)
                        return DatabaseType.GteSqlServer2012;
                    return DatabaseType.SqlServer;
                });
            }
#if ASP_NET_CORE
            if (connection is Microsoft.Data.Sqlite.SqliteConnection)
#else
            if (connection is System.Data.SQLite.SQLiteConnection)
#endif
                return DatabaseType.SQLite;

            if (connection is Npgsql.NpgsqlConnection)
                return DatabaseType.PostgreSQL;

            return DatabaseType.MySql;
        }

        /// <summary>
        /// 获取值
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="propertyInfo"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        internal static object GetValueFromExpression<TEntity>(this PropertyInfo propertyInfo, Expression<Func<TEntity, bool>> expression)
        {
            var dictionary = new Dictionary<object, object>();
            ExpressionHelper.Resolve(expression.Body, ref dictionary);
            if ((dictionary?.Count ?? 0) <= 0)
                throw new ArgumentNullException($"Property [{propertyInfo.Name}] 数据为空");

            dictionary.TryGetValue(propertyInfo.Name, out object val);
            return val;
        }

        /// <summary>
        /// 获取位数
        /// </summary>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        [Obsolete("请使用TableNameFunc")]
        internal static int GetBit(this PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                return -1;

            var bit = ((SubmeterAttribute)propertyInfo.GetCustomAttribute(typeof(SubmeterAttribute)))?.Bit ?? -1;
            return bit;
        }

        /// <summary>
        /// 获取后缀
        /// </summary>
        /// <param name="val"></param>
        /// <param name="bit"></param>
        /// <returns></returns>
        [Obsolete("请使用TableNameFunc")]
        internal static string GetSuffix(string val, int bit = 2)
        {
            if (string.IsNullOrEmpty(val))
                throw new ArgumentNullException($"分表数据为空");
            if (bit <= 0)
                throw new ArgumentOutOfRangeException("length", "length必须是大于零的值。");

            var result = Encoding.Default.GetBytes(val.ToString());    //tbPass为输入密码的文本框
            var md5Provider = new MD5CryptoServiceProvider();
            var output = md5Provider.ComputeHash(result);
            var hash = BitConverter.ToString(output).Replace("-", "");  //tbMd5pass为输出加密文本

            var suffix = hash.Substring(0, bit).ToUpper();
            return suffix;
        }

        /// <summary>
        /// 获取分表名 base md5
        /// </summary>
        /// <param name="propertyInfo"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        [Obsolete("请使用TableNameFunc")]
        internal static string GetSuffix(this PropertyInfo propertyInfo, string val)
        {
            var bit = propertyInfo.GetBit();
            return GetSuffix(val.ToString(), bit);
        }

        /// <summary>
        /// 获取分表名 base md5
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="propertyInfo"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        [Obsolete("请使用TableNameFunc")]
        internal static string GetSuffix<TEntity>(this PropertyInfo propertyInfo, TEntity entity) where TEntity : class, new()
        {
            var val = propertyInfo.GetValue(entity);
            var bit = propertyInfo.GetBit();
            return GetSuffix(val.ToString(), bit);
        }

        /// <summary>
        /// 获取分表名 base md5
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="propertyInfo"></param>
        /// <param name="expression">表达式数据</param>
        /// <returns></returns>
        [Obsolete("请使用TableNameFunc")]
        internal static string GetSuffix<TEntity>(this PropertyInfo propertyInfo, Expression<Func<TEntity, bool>> expression) where TEntity : class, new()
        {
            var val = propertyInfo.GetValueFromExpression(expression);
            var bit = propertyInfo.GetBit();
            return GetSuffix(val.ToString(), bit);
        }
        #endregion
    }
}
